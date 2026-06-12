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

    /// <summary>Creates the movement-action handler over the movement and board-resolution seams.</summary>
    public MovementActionService(MovementService movementService,
        BoardService boardService)
    {
        _movementService = movementService;
        _boardService = boardService;
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
    public async Task ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, MovementAction action, CancellationToken ct)
    {
        if (action.Kind == MovementKind.Swap)
        {
            await ApplySwap(engine, player, action, ct);
            return;
        }

        var targets = await CardActionHelper.ResolveTargets(engine, player, action.Target, ct);
        foreach (var target in targets)
        {
            switch (action.Kind)
            {
                case MovementKind.MoveSpaces:
                    // Advance/retreat N spaces; CollectGoBonus=false suppresses the GO bonus on a
                    // "do not pass Go" card. (Advance/AdvanceToNearest carry it via MovementDirection.)
                    await _movementService.MovePlayer(engine, target, action.Spaces, ct, action.CollectGoBonus);
                    break;
                case MovementKind.AdvanceToIndex when action.TargetIndex is { } index:
                    await _movementService.AdvancePlayer(engine, target, index, MovementDirection(action), ct);
                    break;
                case MovementKind.AdvanceToNearest:
                    await _movementService.AdvancePlayer(engine, target, FindNearest(target, action.Nearest), MovementDirection(action), ct);
                    break;
                case MovementKind.GoToJustVisiting:
                    await _movementService.AdvancePlayer(engine, target, IndexHelper.JustVisitingSpace,
                        PlayerMovementDirection.CounterDirectionOfTravel, ct);
                    continue;   // Just Visiting performs no space action
                default:
                    continue;
            }

            // Perform the landed space's action (rent, GO, tax, ...) unless the card suppresses it.
            if (action.ResolveLandedSpace)
                await _boardService.ResolveBoardSpaceForPlayer(engine, target, ct);
        }
    }
    
    
    /// <summary>
    /// A swap exchanges the holder's and one chosen player's board positions — no GO bonus, and no
    /// landed-space action (game-rules.md Movement rule 4).
    /// </summary>
    private async Task ApplySwap(Framework.GameEngine engine, PlayerModel player, MovementAction action, CancellationToken ct)
    {
        var pick = action.Target == PlayerTarget.Self ? PlayerTarget.ChosenPlayer : action.Target;
        var target = (await CardActionHelper.ResolveTargets(engine, player, pick, ct)).FirstOrDefault();
        if (target is null || target.PlayerId == player.PlayerId)
            return;

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
    /// Scans forward in the player's facing direction for the nearest space of
    /// <paramref name="kind"/> (station / utility / buildable property), returning its board index.
    /// Falls back to the player's current index if none is found (a board always holds the kind).
    /// </summary>
    private static ushort FindNearest(PlayerModel player, NearestKind kind)
    {
        var targets = kind switch
        {
            NearestKind.Station => PropertySetHelper.StationIndexes,
            NearestKind.Utility => PropertySetHelper.UtilityIndexes,
            _ => IndexHelper.BuildablePropertyIndexes
        };

        var index = player.BoardIndex;
        for (var step = 0; step < IndexHelper.PhysicalBoardSize; step++)
        {
            (index, _) = IndexHelper.MoveIndex(index, 1, player.Direction);
            if (targets.Contains(index))
                return index;
        }
        return player.BoardIndex;   // fallback — a board always holds the target kind
    }
}