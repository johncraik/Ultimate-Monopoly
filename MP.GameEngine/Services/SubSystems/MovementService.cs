using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class MovementService
{
    private readonly BoardService _boardService;

    public MovementService(BoardService boardService)
    {
        _boardService = boardService;
    }
    
    public async Task MovePlayer(Framework.GameEngine engine, PlayerModel player, int amount, CancellationToken ct)
    {
        var (newIndex, goPasses) = IndexHelper.MoveIndex(player.BoardIndex, amount, player.Direction);
        var initial = player.BoardIndex;
        player.BoardIndex = newIndex;

        if (goPasses > 0 && amount > 0)
        {
            //Can only collect GO money if moving in direction of travel (positive amount)
            //TODO call go service to apply go passes
        }

        engine.EventEmitter.Emit(new PlayerMovedReceipt
        {
            PlayerId = player.PlayerId,
            InitialBoardIndex = initial,
            FinalBoardIndex = player.BoardIndex,
            Direction = amount > 0 ? PlayerMovementDirection.DirectionOfTravel : PlayerMovementDirection.CounterDirectionOfTravel,
            IsAdvance = false
        });
        await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
    }

    public async Task AdvancePlayer(Framework.GameEngine engine, PlayerModel player, ushort boardIndex, PlayerMovementDirection direction, 
        CancellationToken ct)
    {
        var (newIndex, passGo) = IndexHelper.AdvanceIndex(player.BoardIndex, boardIndex, player.Direction);
        var initial = player.BoardIndex;
        player.BoardIndex = newIndex;

        if (passGo && direction == PlayerMovementDirection.DirectionOfTravel)
        {
            //Can only collect GO money if moving in direction of travel
            //TODO call go service to apply go passes
        }
        
        engine.EventEmitter.Emit(new PlayerMovedReceipt
        {
            PlayerId = player.PlayerId,
            InitialBoardIndex = initial,
            FinalBoardIndex = player.BoardIndex,
            Direction = direction,
            IsAdvance = true
        });
        await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
    }

    public async Task SendPlayerToJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        player.JailTurnCounter = 0;
        await AdvancePlayer(engine, player, IndexHelper.JailSpace, PlayerMovementDirection.CounterDirectionOfTravel, ct);
    }
}