using MP.GameEngine.Enums;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class TaxService
{
    private readonly TransactionService _transactionService;

    public TaxService(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }


    public async Task PayTax(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var index = player.BoardIndex;
        if(!index.IsTax())
            return;
        
        var space = engine.Cache.Board.GetBoardSpace(index);
        if(!space.IsTaxable || space.Tax == null)
            return;
        
        //TODO get a tax card (changes outcome)
        
        //Default outcome (normal tax payment):
        var tax = MoneyHelper.NormaliseAmountToPositive((long)space.Tax, engine.Cache.RoundingRule, FinancialReason.Tax);
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, space.Name, 
            $"You will pay {RuleDictionary.Currency}{tax} in tax.", ct: ct);
        
        await _transactionService.PayTax(engine, player, tax, ct);
    }
}