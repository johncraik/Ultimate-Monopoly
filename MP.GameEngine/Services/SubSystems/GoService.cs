using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Cards;

namespace MP.GameEngine.Services.SubSystems;

public class GoService
{
    private readonly TransactionService _transactionService;
    private readonly LoanService _loanService;
    private readonly PropertyCommandService _propCommandService;
    private readonly CardTriggerService _triggerService;

    public GoService(TransactionService transactionService,
        LoanService loanService,
        PropertyCommandService propCommandService,
        CardTriggerService triggerService)
    {
        _transactionService = transactionService;
        _loanService = loanService;
        _propCommandService = propCommandService;
        _triggerService = triggerService;
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
        
        var passGoSuppress = await _triggerService.OnPassGo(engine, player, bonus, ct);
        var otherPassGoSuppress = await _triggerService.OnOtherPassGo(engine, player, bonus, ct);
        
        var sd = new SuppressDefault(passGoSuppress.Type());
        sd.Aggregate(otherPassGoSuppress);
        if(!sd.SuppressGoBonus)
        {
            //Cite rule and give GO bonus:
            engine.CiteRule(player.Direction == PlayerDirection.Forward ? RuleCode.Go_PassClockwise : RuleCode.Go_PassAntiClockwise);
            await _transactionService.ReceiveGoBonus(engine, player, bonus, ct);
        }
        
        //Pay mortage fee (no-ops if no mortgages):
        await _propCommandService.PayMortgageFee(engine, player, ct);
        
        //Repay any loans (no-ops if no loans):
        await _loanService.ForcedRepayLoans(engine, player, ct);

        //Player has now passed GO (assumed since they are collecting bonus)
        player.HasPassedInitialGo = true;
    }

    public async Task LandOnGo(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var triggerSuppress = await _triggerService.OnLandGo(engine, player, RuleDictionary.LandOnGoBonus, ct);
        var suppressDefault = await engine.CardService.DrawCard(engine, player, CardType.Go, ct);

        var sd = new SuppressDefault(triggerSuppress.Type());
        sd.Aggregate(suppressDefault);
        if(sd.SuppressGoBonus) return;
        
        //Cite rule and notify user:
        engine.CiteRule(RuleCode.Go_LandOn);
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "GO", 
            $"You have landed on GO and will collect {RuleDictionary.Currency}{RuleDictionary.LandOnGoBonus}.", ct: ct);
        
        await _transactionService.ReceiveGoBonus(engine, player, RuleDictionary.LandOnGoBonus, ct);
    }
}