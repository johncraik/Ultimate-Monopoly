using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class MovementService
{
    private readonly GoService _goService;

    public MovementService(GoService goService)
    {
        _goService = goService;
    }
    
    public async Task MovePlayer(Framework.GameEngine engine, PlayerModel player, int amount, CancellationToken ct,
        bool collectGoBonus = true)
    {
        if(player.IsInJail)
            return;

        var (newIndex, goPasses) = IndexHelper.MoveIndex(player.BoardIndex, amount, player.Direction);
        var initial = player.BoardIndex;
        player.BoardIndex = newIndex;

        // collectGoBonus is false for a "do not pass Go, do not collect £200" card — it suppresses
        // the whole GO-pass consequence (bonus, mortgage fee, loan instalment). Default true: normal
        // movement (rolls, third die, doubles) always collects when crossing GO in the travel direction.
        if (collectGoBonus && goPasses > 0 && amount > 0 && !player.InitialRoll)
            //Can only collect GO money if moving in direction of travel (positive amount)
            await _goService.CollectGoMoney(engine, player, goPasses, ct);

        engine.EventEmitter.Emit(new PlayerMovedReceipt
        {
            PlayerId = player.PlayerId,
            InitialBoardIndex = initial,
            FinalBoardIndex = player.BoardIndex,
            Direction = amount > 0 ? PlayerMovementDirection.DirectionOfTravel : PlayerMovementDirection.CounterDirectionOfTravel,
            IsAdvance = false
        });
    }

    public async Task AdvancePlayer(Framework.GameEngine engine, PlayerModel player, ushort boardIndex, PlayerMovementDirection direction,
        CancellationToken ct, bool willResolveSpace = true)
    {
        switch (player.IsInJail)
        {
            // A jailed player cannot be advanced out of jail. The only advance that legitimately moves
            // them is the release to Just Visiting (rolling a double, paying/playing out, or a
            // mass-breakout card) — every other advance (a card "advance to X"/"go back to X", "go to
            // jail" while already jailed, etc.) no-ops and leaves them in jail. Mirrors the IsInJail
            // guard in MovePlayer. Send-to-jail is unaffected: the player isn't jailed yet at that point.
            case true when boardIndex != IndexHelper.JustVisitingSpace:
                return;
            case true:
                engine.Notifier.Notify(engine.Cache.GameId, player.PlayerId, "You have been released from jail");
                break;
        }

        var (newIndex, passGo) = IndexHelper.AdvanceIndex(player.BoardIndex, boardIndex, player.Direction);
        var initial = player.BoardIndex;
        player.BoardIndex = newIndex;

        if (passGo && direction == PlayerMovementDirection.DirectionOfTravel)
            //Can only collect GO money if moving in direction of travel
            await _goService.CollectGoMoney(engine, player, 1, ct);
        else if (newIndex == IndexHelper.GoSpace)
            //A deliberate advance ONTO GO reaches GO, so the initial-GO lock is satisfied
            //(game-rules.md Movement rule 2 / GO Space rule 4). AdvanceIndex returns
            //passesGo:false for a GO destination, so CollectGoMoney never runs here — and
            //it normally owns this assignment. The £200 land bonus is paid separately by the
            //landed-space resolution (GoService.LandOnGo). This is the advance path only: a
            //double-3 wobble back to GO uses MovePlayer, so it never releases the lock.
            player.HasPassedInitialGo = true;

        engine.EventEmitter.Emit(new PlayerMovedReceipt
        {
            PlayerId = player.PlayerId,
            InitialBoardIndex = initial,
            FinalBoardIndex = player.BoardIndex,
            Direction = direction,
            IsAdvance = true
        });
        
        //Prevent card if specific space:
        if(willResolveSpace &&  
           (newIndex == IndexHelper.GoSpace 
           || newIndex == IndexHelper.JustVisitingSpace
           || newIndex == IndexHelper.FreeParkingSpace
           || newIndex == IndexHelper.GoToJailSpace
           || IndexHelper.TaxIndexes.Contains(newIndex)))
            engine.Cache.Prevent(player.PlayerId, newIndex);
    }

    public async Task SendPlayerToJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        player.JailTurnCounter = 0;
        await AdvancePlayer(engine, player, IndexHelper.JailSpace, PlayerMovementDirection.CounterDirectionOfTravel, ct);
    }
}