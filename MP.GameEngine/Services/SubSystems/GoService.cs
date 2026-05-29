using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class GoService
{
    private readonly TransactionService _transactionService;

    public GoService(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }
    
    public async Task CollectGoMoney(Framework.GameEngine engine, PlayerModel player, ushort goPasses, CancellationToken ct)
    {
        var bonus = player.Direction switch
        {
            PlayerDirection.Forward => RuleDictionary.GoPassClockwiseBonus,
            PlayerDirection.Backward => RuleDictionary.GoPassCounterClockwiseBonus,
            _ => throw new ArgumentOutOfRangeException(nameof(player), player, null)
        };
        
        bonus *= goPasses;
        await _transactionService.ReceiveGoBonus(engine, player, bonus, ct);
    }
}