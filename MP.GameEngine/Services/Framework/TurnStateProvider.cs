using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
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
public class TurnStateProvider(GameCacheModel cache) : ITurnStateProvider
{
    // ─── Private primitives ──────────────────────────────────────────────

    /// <summary>True when no prompt is awaiting a response — i.e. the engine is not mid-execution.</summary>
    private bool IsEngineIdle() => cache.PendingPrompt is null;

    /// <summary>True when the given player is the one whose turn it currently is.</summary>
    private bool IsCurrentPlayer(string playerId) =>
        cache.Game.Metadata.CurrentPlayerId == playerId;
    
    private PlayerModel CurrentPlayer()
        => cache.Game.Players.FirstOrDefault(p => p.PlayerId == cache.Game.Metadata.CurrentPlayerId) 
           ?? throw new InvalidOperationException("Current player not found in game players list.");

    /// <summary>True when the given player is in jail right now.</summary>
    private bool IsJailed(string playerId)
    {
        var player = cache.Game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        return player?.IsInJail ?? throw new InvalidOperationException("Player not found in game players list.");
    }

    /// <summary>True when the cache is at one of the two idle boundary phases (Start or End of turn).</summary>
    private bool IsAtTurnBoundary() =>
        cache.TurnState is TurnState.StartOfTurn or TurnState.EndOfTurn;


    // ─── Capability gates ───────────────────────────────────────────────
    // Each "Can…" returns true when the named player is allowed to issue
    // the command right now. Composes the primitives above — no boolean
    // spaghetti in callers.

    /// <summary>
    /// Portfolio commands (mortgage / unmortgage / build / sell houses / play
    /// card from hand / pay loan early — anything in the player's portfolio):
    /// StartOfTurn only, current player only, not in jail, engine idle.
    /// EndOfTurn is *not* a portfolio window — once movement is done the
    /// player can only end the turn or initiate/accept a deal.
    /// </summary>
    public bool CanPortfolioCommand(string playerId) =>
        cache.TurnState == TurnState.StartOfTurn
        && IsCurrentPlayer(playerId)
        && !IsJailed(playerId)
        && IsEngineIdle();

    /// <summary>
    /// Deals can be initiated or accepted at either turn boundary (Start or
    /// End). Bilateral validation (the *other* party must also be reachable)
    /// is the engine layer's job — the provider only confirms the calling
    /// player is at a legal moment in their own turn cycle.
    /// </summary>
    public bool CanDeal(string playerId) =>
        IsAtTurnBoundary() && IsEngineIdle();

    /// <summary>
    /// Jail exit (pay fee / play card / attempt double): only at StartOfTurn,
    /// only by the current player, only if they're actually in jail.
    /// </summary>
    public bool CanLeaveJail(string playerId) =>
        cache.TurnState == TurnState.StartOfTurn
        && IsCurrentPlayer(playerId)
        && IsJailed(playerId)
        && IsEngineIdle();

    /// <summary>End turn: current player only, at EndOfTurn, engine idle.</summary>
    public bool CanEndTurn(string playerId) =>
        cache.TurnState == TurnState.EndOfTurn
        && IsCurrentPlayer(playerId)
        && IsEngineIdle();

    /// <summary>
    /// Voluntary bankruptcy: any player, at either turn boundary, engine idle.
    /// "At any time" in <c>game-rules.md</c> Bankruptcy rule 1 means at any
    /// of their own (or another player's) turn boundaries — not literally any
    /// moment in the middle of execution.
    /// </summary>
    public bool CanDeclareBankruptcy(string playerId) =>
        IsAtTurnBoundary() && IsEngineIdle();


    // ─── Transitions ────────────────────────────────────────────────────
    // Named transitions encode the branches of the turn loop. Both
    // extra-turn and next-player fire from EndOfTurn, commit, and return a
    // GameModel snapshot — they differ in whether TurnNumber / CurrentPlayerId
    // advance. Each transition validates the current state before mutating,
    // throwing if called from the wrong place.

    /// <summary>StartOfTurn → PlayerRollMovement. The player has finished any portfolio commands and is rolling.</summary>
    public void TransitionToRollPhase()
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
    /// 6), and returns the resulting <see cref="GameModel"/> for the caller
    /// to persist as a new <c>GameSnapshot</c> row. The
    /// <c>GameTurn</c> record (and therefore <c>CurrentTurnId</c>,
    /// <c>TurnNumber</c>, and <c>CurrentPlayerId</c>) is **unchanged** —
    /// extra-turn snapshots sit under the same GameTurn. A new GameTurn is
    /// only created by <see cref="TransitionToNextPlayer"/>.
    /// </summary>
    public GameModel TransitionToExtraTurn(bool isTriple)
    {
        Expect(TurnState.EndOfTurn);

        var player = CurrentPlayer();
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

        cache.SaveChanges();
        cache.ClearEvents();
        cache.SetTurnState(TurnState.StartOfTurn);

        return cache.Game;
    }

    /// <summary>
    /// EndOfTurn → StartOfTurn (for the next player). Commits the working
    /// game state, clears the per-turn event list, and returns the resulting
    /// <see cref="GameModel"/> — the snapshot for the turn just beginning.
    /// The caller is responsible for persisting the returned snapshot and
    /// broadcasting it (the engine knows nothing about storage — see
    /// <c>game-engine.md</c> §3).
    /// </summary>
    public GameModel TransitionToNextPlayer()
    {
        Expect(TurnState.EndOfTurn);

        AdvancePlayer();

        cache.SaveChanges();
        cache.ClearEvents();
        cache.SetTurnState(TurnState.StartOfTurn);

        return cache.Game;
    }


    // ─── Internals ──────────────────────────────────────────────────────

    private void Expect(TurnState expected)
    {
        if (cache.TurnState != expected)
            throw new InvalidOperationException(
                $"Transition requires TurnState={expected}, got {cache.TurnState}.");
    }

    /// <summary>
    /// Advances <see cref="TurnMetadata.CurrentPlayerId"/> to the next
    /// eligible player and bumps the turn counters. Intrinsic to "next
    /// player" but borders on game logic — kept as a stub for now;
    /// real implementation needs to skip bankrupt players
    /// (<c>game-rules.md</c> Bankruptcy), decrement
    /// <see cref="PlayerModel.TurnsToMiss"/> and skip missed-turn players
    /// (Double 2 effect), and generate a new <c>CurrentTurnId</c>.
    /// </summary>
    private void AdvancePlayer()
    {
        var currentPlayer = CurrentPlayer();
        var allPlayers = cache.Game.Players.OrderBy(p => p.OrderId).ToList();
        
        var nextPlayer = allPlayers.FirstOrDefault(p => p.OrderId > currentPlayer.OrderId) 
                         ?? allPlayers.MinBy(p => p.OrderId)
                         ?? throw new InvalidOperationException("No eligible players left in game.");

        //Turn ID is not set, since turn ID is the DB key for game turn info
        cache.Game.Metadata.CurrentPlayerId = nextPlayer.PlayerId;
        cache.Game.Metadata.TurnNumber++;
    }
}