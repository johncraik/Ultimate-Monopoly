using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.Framework;

/// <summary>
/// Owns turn-state capability checks and transitions for a single game
/// (one cache, one provider). All command-gating questions and all phase
/// transitions go through here — outside code should never compare
/// <see cref="GameCacheModel.TurnState"/> directly. See
/// <c>design-docs/turn-state.md</c>.
/// </summary>
/// <remarks>
/// The provider is a stateful helper, not the orchestrator. Higher-level
/// turn-loop / caller services decide *when* to call the transitions; the
/// provider only owns the rules of *what is allowed* and *what the next
/// state is*.
/// </remarks>
public class TurnStateProvider(GameCacheModel cache, ISnapshotService snapshotService) : ITurnStateProvider
{
    // ─── Private primitives ──────────────────────────────────────────────

    /// <summary>True when no prompt is awaiting a response — i.e. the engine is not mid-execution.</summary>
    private bool IsEngineIdle() => cache.PendingPrompt is null;

    /// <summary>True when the given player is the one whose turn it currently is.</summary>
    private bool IsCurrentPlayer(string playerId) =>
        cache.Game.Metadata.CurrentPlayerId == playerId;

    /// <summary>True when the given player is in jail right now.</summary>
    private bool IsJailed(string playerId)
    {
        var player = cache.Game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        return player?.IsInJail ?? throw new InvalidOperationException("Player not found in game players list.");
    }

    /// <summary>True when the player exists in the game and is still in play (not bankrupt).</summary>
    private bool IsActivePlayer(string playerId) =>
        cache.Game.GetPlayer(playerId) != null;

    /// <summary>True when the cache is at one of the two idle boundary phases (Start or End of turn).</summary>
    private bool IsAtTurnBoundary() =>
        cache.TurnState is TurnState.StartOfTurn or TurnState.EndOfTurn;

    /// <summary>
    /// True when <paramref name="submittingUserId"/> may act for
    /// <paramref name="playerId"/> — the player acting for themselves, or the
    /// host acting on their behalf. The host tablet is the game controller and
    /// can drive any player's commands; phones are an optional convenience
    /// layer. See <c>design-docs/Game-UI.md</c>.
    /// </summary>
    private bool IsAuthorisedActor(string playerId, string submittingUserId) =>
        submittingUserId == playerId || submittingUserId == cache.HostPlayerId;


    // ─── Capability gates ───────────────────────────────────────────────
    // Each "Can…" returns true when the submitting user may issue the command
    // for the named player right now. Game-state checks key off the named
    // player; authorisation is the player themselves or the host (host-bypass).
    // Composes the primitives above — no boolean spaghetti in callers.

    /// <summary>
    /// Start turn (roll dice): allowed whenever the turn is at StartOfTurn.
    /// No actor gating — kicking off the current player's turn is open to the
    /// table (the host drives it on the tablet).
    /// </summary>
    public bool CanStartTurn(string playerId, string submittingUserId) => 
        IsAuthorisedActor(playerId, submittingUserId) 
        && IsActivePlayer(playerId)
        && cache.TurnState == TurnState.StartOfTurn
        && IsEngineIdle();

    /// <summary>
    /// Portfolio commands (mortgage / unmortgage / build / sell houses / play
    /// card from hand / pay loan early — anything in the player's portfolio):
    /// StartOfTurn only, current player only, not in jail, engine idle.
    /// EndOfTurn is *not* a portfolio window — once movement is done the
    /// player can only end the turn or initiate/accept a deal.
    /// </summary>
    public bool CanPortfolioCommand(string playerId, string submittingUserId) =>
        IsAuthorisedActor(playerId, submittingUserId)
        && cache.TurnState == TurnState.StartOfTurn
        && IsCurrentPlayer(playerId)
        && !IsJailed(playerId)
        && IsEngineIdle();

    /// <summary>
    /// Deals can be initiated or accepted at either turn boundary (Start or End) by ANY active
    /// (non-bankrupt) player — not just the current player: a deal fires at a turn boundary regardless
    /// of whose turn it is. Bilateral validation (the *other* party must also be reachable) is the engine
    /// layer's job — the provider only confirms the proposer is an active player and the game is sitting
    /// at a turn boundary (engine idle).
    /// </summary>
    public bool CanDeal(string playerId, string submittingUserId) =>
        IsAuthorisedActor(playerId, submittingUserId)
        && IsActivePlayer(playerId)
        && IsAtTurnBoundary()
        && IsEngineIdle();

    /// <summary>
    /// Jail exit (pay fee / play card / attempt double): only at StartOfTurn,
    /// only by the current player, only if they're actually in jail.
    /// </summary>
    public bool CanLeaveJail(string playerId, string submittingUserId) =>
        IsAuthorisedActor(playerId, submittingUserId)
        && cache.TurnState == TurnState.StartOfTurn
        && IsCurrentPlayer(playerId)
        && IsJailed(playerId)
        && (cache.Game.GetPlayer(playerId)?.CanLeaveJail ?? false)
        && IsEngineIdle();

    /// <summary>End turn: current player only, at EndOfTurn, engine idle.</summary>
    public bool CanEndTurn(string playerId, string submittingUserId) =>
        IsAuthorisedActor(playerId, submittingUserId)
        && cache.TurnState == TurnState.EndOfTurn
        && IsCurrentPlayer(playerId)
        && IsEngineIdle();

    /// <summary>
    /// Voluntary bankruptcy: any player, at either turn boundary, engine idle.
    /// "At any time" in <c>game-rules.md</c> Bankruptcy rule 1 means at any
    /// of their own (or another player's) turn boundaries — not literally any
    /// moment in the middle of execution.
    /// </summary>
    public bool CanDeclareBankruptcy(string playerId, string submittingUserId) =>
        IsAuthorisedActor(playerId, submittingUserId)
        && IsActivePlayer(playerId)
        && IsAtTurnBoundary() 
        && IsEngineIdle();


    // ─── Transitions ────────────────────────────────────────────────────
    // Named transitions encode the branches of the turn loop. Both
    // extra-turn and next-player fire from EndOfTurn, commit, and write a
    // new GameTurn + GameSnapshot pair — they differ only in whether
    // CurrentPlayerId advances (next-player) or stays (extra-turn). Each
    // transition validates the current state before mutating, throwing
    // if called from the wrong place.

    /// <summary>StartOfTurn → PlayerRollMovement. The player has finished any portfolio commands and is rolling.</summary>
    public void TransitionToRollMovementPhase()
    {
        Expect(TurnState.StartOfTurn);
        cache.SetTurnState(TurnState.PlayerRollMovement);
    }

    /// <summary>
    /// PlayerRollMovement → ThirdDieMovement. Normal roll or non-triple
    /// double — the roller has moved and now the other players take the
    /// third die.
    /// </summary>
    public void TransitionToThirdDie()
    {
        Expect(TurnState.PlayerRollMovement);
        cache.SetTurnState(TurnState.ThirdDieMovement);
    }
    
    /// <summary>
    /// → EndOfTurn. Allowed from PlayerRollMovement (triple with no extra
    /// roll due, or 3-in-a-row sending the roller to jail) or
    /// ThirdDieMovement (normal end of turn — other players have moved and
    /// no extra roll was triggered).
    /// </summary>
    public void TransitionToEndOfTurn()
    {
        if (cache.TurnState is not (TurnState.PlayerRollMovement or TurnState.ThirdDieMovement))
            throw new InvalidOperationException(
                $"TransitionToEndOfTurn requires PlayerRollMovement or ThirdDieMovement, got {cache.TurnState}.");
        
        cache.SetTurnState(TurnState.EndOfTurn);
    }

    /// <summary>
    /// EndOfTurn → StartOfTurn for the *same* player — an extra turn
    /// granted by a double, triple, or card. Fires from EndOfTurn (not
    /// directly from the movement phases), so the player has had the
    /// EndOfTurn idle window to settle deals before rolling their extra
    /// turn. Commits the working state, clears the per-turn event window,
    /// bumps the matching <c>DoublesInRow</c> / <c>TriplesInRow</c> counter
    /// (and resets the other per <c>game-rules.md</c> Doubles/Triples rule
    /// 6), advances turn metadata (new <c>CurrentTurnId</c>,
    /// <c>TurnNumber</c>++; <c>CurrentPlayerId</c> **unchanged**), and
    /// writes a snapshot via <see cref="ISnapshotService.CreateSnapshotAsync"/>
    /// — a new <c>GameTurn</c> row plus its <c>GameSnapshot</c>. At the
    /// schema level the only thing distinguishing an extra-turn record from
    /// a next-player record is that consecutive <c>GameTurn</c> rows share
    /// <c>CurrentPlayerId</c>; see <see cref="TransitionToNextPlayer"/> for
    /// the other path. The engine does not know *how* the snapshot is
    /// persisted — only that one is taken at this boundary (see
    /// <c>game-engine.md</c> §3).
    /// </summary>
    public async Task TransitionToExtraTurn(bool isTriple)
    {
        Expect(TurnState.EndOfTurn);
        cache.Game.ModifiedDiceRollType = null;

        var player = cache.Game.CurrentPlayer();
        if (player == null) throw new InvalidOperationException("Current player not found in game players list.");
        
        if (isTriple)
        {
            player.TriplesInRow++;
            player.DoublesInRow = 0;
        }
        else
        {
            player.DoublesInRow++;
            player.TriplesInRow = 0;
        }
        
        ClearBuiltOnTurnFlags();
        UpdateMetadata(player.PlayerId);
        
        //Store the CURRENT turn ID (for event snapshot);
        //when snapshot is created, a new game turn is created, changing the ID
        var turnId = cache.Game.Metadata.CurrentTurnId;
        await snapshotService.CreateSnapshotAsync(cache.Game);
        await snapshotService.CreateTurnEventSnapshotAsync(cache.GameId, turnId, cache.Events.ToList());
        
        cache.ClearEvents();
        cache.SaveChanges();
    }

    /// <summary>
    /// EndOfTurn → StartOfTurn (for the next player). Commits the working
    /// game state, clears the per-turn event list, and writes a snapshot
    /// via <see cref="ISnapshotService.CreateSnapshotAsync"/> — a new
    /// <c>GameTurn</c> row plus its <c>GameSnapshot</c> for the turn just
    /// beginning. The engine does not know *how* the snapshot is persisted
    /// — only that one is taken at this boundary (see
    /// <c>game-engine.md</c> §3). Broadcasting is the caller's job;
    /// post-call state can be read from <c>cache.Game</c>.
    /// </summary>
    public async Task TransitionToNextPlayer()
    {
        Expect(TurnState.EndOfTurn);
        cache.Game.ModifiedDiceRollType = null;

        ClearBuiltOnTurnFlags();
        AdvancePlayer();
        
        //Store the CURRENT turn ID (for event snapshot);
        //when snapshot is created, a new game turn is created, changing the ID
        var turnId = cache.Game.Metadata.CurrentTurnId;
        await snapshotService.CreateSnapshotAsync(cache.Game);
        await snapshotService.CreateTurnEventSnapshotAsync(cache.GameId, turnId, cache.Events.ToList());
        
        cache.ClearEvents();
        cache.SaveChanges();
    }

    public async Task TransitionToFinalTurn()
    {
        ClearBuiltOnTurnFlags();
        cache.Game.ModifiedDiceRollType = null;
        
        var lastPlayer = cache.Game.GetPlayers(excludePovPlayer: false)
            .FirstOrDefault();
        if(lastPlayer == null)
            throw new InvalidOperationException("No players in game.");
        
        UpdateMetadata(lastPlayer.PlayerId);
        
        cache.SetTurnState(TurnState.EndOfTurn);
        //Set to a finished state, so last snapshot (final turn snapshot) has a state of finished,
        //Final turn snapshot is just to save the outcome of the previous turn; and conclude the game
        cache.GameState = GameState.Finished;
        
        //Store the CURRENT turn ID (for event snapshot);
        //when snapshot is created, a new game turn is created, changing the ID
        var turnId = cache.Game.Metadata.CurrentTurnId;
        await snapshotService.CreateSnapshotAsync(cache.Game, finalTurn: true);
        await snapshotService.CreateTurnEventSnapshotAsync(cache.GameId, turnId, cache.Events.ToList());
        
        cache.ClearEvents();
        cache.SaveChanges();
    }


    // ─── Internals ──────────────────────────────────────────────────────

    private void Expect(TurnState expected)
    {
        if (cache.TurnState != expected)
            throw new InvalidOperationException(
                $"Transition requires TurnState={expected}, got {cache.TurnState}.");
    }

    /// <summary>
    /// Advances <see cref="TurnMetadata.CurrentPlayerId"/> clockwise to the
    /// next eligible player. Bankrupt players are skipped (excluded by
    /// <see cref="GameModel.GetPlayers(bool,bool)"/>); players who
    /// must miss a turn are skipped and have their
    /// <see cref="PlayerModel.TurnsToMiss"/> decremented as they are passed
    /// (Double 2 / Double 5 effects). When every other player is skipping,
    /// play returns to the current player.
    /// </summary>
    private void AdvancePlayer()
    {
        //Grabs ordered list of players from current player POV
        var otherPlayers = cache.Game.GetPlayers(excludePovPlayer: false);
        PlayerModel? nextPlayer = null;

        var currentPlayer = cache.Game.CurrentPlayer();
        if (currentPlayer is { HasExtraTurns: true })
        {
            //The current player has extra turns (and no turns to miss)
            //Does not throw when current player is not found in the list, as they may have bankrupted on this turn,
            //and thus, loose their extra turns anyway
            currentPlayer.ExtraTurns--;
            nextPlayer = currentPlayer;
        }
        else
        {
            if(otherPlayers.Count == 0)
                throw new InvalidOperationException("No players in game.");
            
            var initialPass = currentPlayer != null;
            do
            {
                //Foreach inside a do while so that this loop will run until a next player is chosen
                //Initial pass variable blocks the current player becoming the next player
                //ONLY when executing first iteration of do while, and first execution of foreach
                foreach (var p in otherPlayers)
                {
                    //Check if player misses this turn
                    //List starts with current player, so prevent setting next player to current player
                    if (!p.MissNextTurn && !initialPass)
                    {
                        //Only sets next player if not first pass (current player)
                        //AND if the player does not miss this turn
                        nextPlayer = p;
                        break;
                    }

                    //If this is the initial pass (current player)
                    //Then do not modify miss turns (it was just their turn)
                    if (initialPass)
                    {
                        //Instead, set false to initial pass and continue to next player in list
                        initialPass = false;
                        continue;
                    }

                    //Only decrement turns to miss if the player is missing this turn,
                    //AND if the player is not the first player in the list
                    p.TurnsToMiss--;
                }
            } while (nextPlayer == null);
        }
        
        UpdateMetadata(nextPlayer.PlayerId);
    }

    private void UpdateMetadata(string playerId)
    {
        //Clears dice roll:
        cache.ClearTurnDiceRoll();
        
        //Clear all rule codes, prevent cards, and set turn state to StartOfTurn
        cache.ClearRuleCodes();
        cache.ClearPrevent();
        cache.SetTurnState(TurnState.StartOfTurn);
        
        // CurrentTurnId is not assigned here — the snapshot service
        // generates the new GameTurn id and writes it back to
        // Metadata.CurrentTurnId as part of CreateSnapshotAsync.
        cache.Game.Metadata.CurrentPlayerId = playerId;
        cache.Game.Metadata.TurnNumber++;
    }

    private void ClearBuiltOnTurnFlags()
    {
        var properties = cache.Game.Properties;
        foreach (var property in properties)
        {
            property.HasBeenBuiltOnThisTurn = false;
        }
    }
}