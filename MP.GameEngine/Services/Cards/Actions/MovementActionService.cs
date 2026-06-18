using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="MovementAction"/> against <see cref="MovementService"/> — board
/// movement (move N spaces, advance to an index, advance to the nearest station/utility/property,
/// move to Just Visiting) or a player position swap. After a move the landed space's action is
/// performed unless the card suppresses it; a swap never performs one (game-rules.md Movement
/// rule 4). See cards-design.md §3.
/// </summary>
public class MovementActionService : ICardActionService<MovementAction>
{
    private readonly MovementService _movementService;
    private readonly BoardService _boardService;
    private readonly JailService _jailService;

    /// <summary>Creates the movement-action handler over the movement, board-resolution and jail seams.</summary>
    public MovementActionService(MovementService movementService,
        BoardService boardService,
        JailService jailService)
    {
        _movementService = movementService;
        _boardService = boardService;
        _jailService = jailService;
    }

    /// <summary>
    /// Resolves the movement for each targeted player: a swap is handled separately; otherwise the
    /// player is moved per <see cref="MovementAction.Kind"/>, then the landed space is resolved
    /// (rent / GO / tax / …) when <see cref="MovementAction.ResolveLandedSpace"/> is set. Just
    /// Visiting is moved-to but never resolved.
    /// </summary>
    /// <param name="engine">The game engine bundle the movement mutates.</param>
    /// <param name="player">The card holder (and default target) being moved.</param>
    /// <param name="action">The movement action to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, MovementAction action, CancellationToken ct, CardActionContext? context = null)
    {
        if (action.Kind == MovementKind.Swap)
        {
            await ApplySwap(engine, player, action, context, ct);
            return true;
        }

        var targets = await CardActionHelper.ResolveTargets(engine, player, action.Target, ct);
        foreach (var target in targets.Where(t => MatchesJailFilter(t, action.JailFilter)))
        {
            switch (action.Kind)
            {
                case MovementKind.MoveSpaces:
                    // Advance/retreat N spaces; CollectGoBonus=false suppresses the GO bonus on a
                    // "do not pass Go" card. (Advance/AdvanceToNearest carry it via MovementDirection.)
                    await _movementService.MovePlayer(engine, target, action.Spaces, ct, action.CollectGoBonus);
                    break;
                case MovementKind.AdvanceToIndex when action.TargetIndex == IndexHelper.JailSpace:
                    // "Go to jail" — route through JailService so the full jail entry happens
                    // (counters reset, JailTurnCounter zeroed, PlayerEnteredJailReceipt + the
                    // "sent to jail" notification), not a bare teleport to index 100.
                    await _jailService.SendPlayerToJail(engine, target, ct);
                    continue;   // jail performs no landed-space action
                case MovementKind.AdvanceToIndex when action.TargetIndex is { } index:
                    await _movementService.AdvancePlayer(engine, target, index, MovementDirection(action), ct);
                    break;
                case MovementKind.AdvanceToNearest:
                    await _movementService.AdvancePlayer(engine, target, FindNearest(engine, target, action), MovementDirection(action), ct);
                    break;
                case MovementKind.GoToJustVisiting:
                    // Honour CollectGoBonus: a card that crosses GO en route to Just Visiting collects the
                    // bonus and unlocks buying (DirectionOfTravel — e.g. "Mishandled evidence"); a "do not
                    // pass GO" card (mass breakout, call a meeting) sets CollectGoBonus=false → counter-
                    // direction, so no bonus and no initial-GO unlock.
                    await _movementService.AdvancePlayer(engine, target, IndexHelper.JustVisitingSpace,
                        MovementDirection(action), ct);
                    continue;   // Just Visiting performs no space action
                default:
                    continue;
            }

            // Perform the landed space's action (rent, GO, tax, ...) unless the card suppresses it.
            if (action.ResolveLandedSpace)
                await _boardService.ResolveBoardSpaceForPlayer(engine, target, ct);
        }

        return true;
    }
    
    
    /// <summary>
    /// A swap exchanges the holder's and one chosen player's board positions — no GO bonus, and no
    /// landed-space action (game-rules.md Movement rule 4).
    /// </summary>
    private async Task ApplySwap(Framework.GameEngine engine, PlayerModel player, MovementAction action, CardActionContext? context, CancellationToken ct)
    {
        PlayerModel? target;
        if (action.Target is PlayerTarget.DiceOffPlayer or PlayerTarget.ContextPlayer)
        {
            // A player an earlier action in this group resolved (e.g. the tax-payer redirect, card 444).
            target = context?.ContextPlayerId is { } id ? engine.Cache.Game.GetPlayer(id) : null;
        }
        else if (action.Target == PlayerTarget.NearestPlayerAhead)
        {
            // Board-relative: the nearest player ahead, same-direction preferred (the FP "ID check" swap).
            target = FindNearestPlayerAhead(engine, player);
        }
        else
        {
            var pick = action.Target == PlayerTarget.Self ? PlayerTarget.ChosenPlayer : action.Target;
            // Default to swapping with a non-jailed player; a card can opt into a jailed target via its
            // JailFilter ("swap places with any other player in jail" → OnlyJailed).
            var filter = action.JailFilter == JailFilter.None ? JailFilter.OnlyNotJailed : action.JailFilter;
            target = (await CardActionHelper.ResolveTargets(engine, player, pick, ct, filter)).FirstOrDefault();
        }

        if (target is null || target.PlayerId == player.PlayerId)
            return;

        // Stash the partner so a later action in the group can act on the same player — e.g. the GO swap's
        // "both players receive £200" grants the swapped player via PlayerTarget.ContextPlayer.
        if (context is not null)
            context.ContextPlayerId = target.PlayerId;

        var holderIndex = player.BoardIndex;
        player.BoardIndex = target.BoardIndex;
        target.BoardIndex = holderIndex;

        engine.EventEmitter.Emit(new PlayerSwappedReceipt
        {
            PlayerId = player.PlayerId,
            SwappedPlayerId = target.PlayerId,
            InitialPlayerBoardIndex = holderIndex,
            FinalPlayerBoardIndex = player.BoardIndex
        });

        // The swapped-in player "proceeds as normal" on the holder's old space (the FP "ID check" swap).
        // The holder, now on the target's old space, performs no landed action (standard swap rule).
        if (action.ResolveLandedSpaceForTarget)
            await _boardService.ResolveBoardSpaceForPlayer(engine, target, ct);
    }

    /// <summary>
    /// The nearest other player ahead of <paramref name="holder"/> on the board, scanning step-by-step in
    /// the holder's travel direction: a player also travelling that direction is preferred (returned as soon
    /// as the nearest such is found), otherwise the nearest player ahead in any direction is the fallback
    /// (cards-dev-changes.md §4). Null when no other player is ahead.
    /// </summary>
    private static PlayerModel? FindNearestPlayerAhead(Framework.GameEngine engine, PlayerModel holder)
    {
        var others = engine.Cache.Game.GetPlayers(holder.PlayerId);
        PlayerModel? nearestAny = null;

        var index = holder.BoardIndex;
        for (var step = 0; step < IndexHelper.PhysicalBoardSize; step++)
        {
            (index, _) = IndexHelper.MoveIndex(index, 1, holder.Direction);
            var here = others.Where(p => p.BoardIndex == index).ToList();
            if (here.Count == 0)
                continue;

            // Same-direction at this step wins outright (it's the nearest same-direction player ahead).
            var sameDirection = here.FirstOrDefault(p => p.Direction == holder.Direction);
            if (sameDirection is not null)
                return sameDirection;

            // Otherwise remember the nearest player ahead (any direction) as the fallback.
            nearestAny ??= here[0];
        }

        return nearestAny;
    }

    /// <summary>
    /// Maps the action's <see cref="MovementAction.CollectGoBonus"/> flag to a movement direction:
    /// travelling in the player's facing direction collects the GO bonus when crossing GO, while a
    /// "do not pass GO" card moves counter to travel so no bonus is paid.
    /// </summary>
    private static PlayerMovementDirection MovementDirection(MovementAction action)
        => action.CollectGoBonus
            ? PlayerMovementDirection.DirectionOfTravel
            : PlayerMovementDirection.CounterDirectionOfTravel;

    /// <summary>
    /// Scans forward in the player's facing direction for the nearest space of the action's
    /// <see cref="MovementAction.Nearest"/> kind (station / utility / buildable property), returning
    /// its board index. When <see cref="MovementAction.NearestOwnedByOther"/> is set, only a space
    /// owned by another player qualifies. Falls back to the player's current index if none is found.
    /// </summary>
    private static ushort FindNearest(Framework.GameEngine engine, PlayerModel player, MovementAction action)
    {
        var targets = action.Nearest switch
        {
            NearestKind.Station => PropertySetHelper.StationIndexes,
            NearestKind.Utility => PropertySetHelper.UtilityIndexes,
            _ => IndexHelper.BuildablePropertyIndexes
        };

        var index = player.BoardIndex;
        for (var step = 0; step < IndexHelper.PhysicalBoardSize; step++)
        {
            (index, _) = IndexHelper.MoveIndex(index, 1, player.Direction);
            if (!targets.Contains(index))
                continue;
            if (action.NearestOwnedByOther && !OwnedByOther(engine, player, index))
                continue;
            return index;
        }
        return player.BoardIndex;   // fallback — a board always holds the target kind
    }

    /// <summary>True when the property at <paramref name="index"/> is owned by a player other than the mover.</summary>
    private static bool OwnedByOther(Framework.GameEngine engine, PlayerModel player, ushort index)
    {
        var property = engine.Cache.Game.GetPropertySpace(index);
        return property?.OwnerPlayerId is not null && property.OwnerPlayerId != player.PlayerId;
    }

    /// <summary>Whether a target passes the action's jail filter (only-jailed / only-not-jailed / no filter).</summary>
    private static bool MatchesJailFilter(PlayerModel player, JailFilter filter)
        => filter switch
        {
            JailFilter.OnlyJailed => player.IsInJail,
            JailFilter.OnlyNotJailed => !player.IsInJail,
            _ => true
        };
}