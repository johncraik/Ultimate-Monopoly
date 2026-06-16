using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Cards.Actions;
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
        
        //Assess the tax due up front — normalised first so the event multiplier scales the grid-rounded
        //figure (and a ×0 "no tax" event floors to 0 → Move's zero short-circuit skips it). The assessed
        //tax is threaded into the card draw as the trigger amount, so an override-on-draw Tax card
        //(AmountSource=TriggerAmount — "triple tax", "pay half") reads the tax due; the same figure is the
        //default if no card supersedes it.
        var tax = MoneyHelper.NormaliseAmountToPositive((long)space.Tax, engine.Cache.RoundingRule, FinancialReason.Tax);
        if (engine.Cache.Game.GlobalEventInfo.TaxEvent)
        {
            //Tax event multiplier (e.g. Tax Rise ×2)
            tax *= engine.Cache.Game.GlobalEventInfo.TaxMultiplier ?? 1;
            engine.CiteRule(RuleCode.Event_Tax);
        }

        var suppressDefault = await engine.CardService.DrawCard(engine, player, CardType.Tax, ct,
            new CardActionContext { TriggerAmount = tax, TriggerReason = FinancialReason.Tax });
        if(suppressDefault.SuppressTaxPayment) return;

        //Default outcome — pay the assessed tax.
        await _transactionService.PayTax(engine, player, tax, ct);
    }
}