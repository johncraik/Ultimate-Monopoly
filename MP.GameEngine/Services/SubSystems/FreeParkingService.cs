using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class FreeParkingService
{
    private readonly TransactionService _transactionService;
    private readonly PropertyTransferService _propertyTransferService;
    private readonly PropertyService _propertyService;
    private readonly PurgingService _purgingService;

    public FreeParkingService(TransactionService transactionService,
        PropertyTransferService propertyTransferService,
        PropertyService propertyService,
        PurgingService purgingService)
    {
        _transactionService = transactionService;
        _propertyTransferService = propertyTransferService;
        _propertyService = propertyService;
        _purgingService = purgingService;
    }

    public async Task PayPropertyFee(Framework.GameEngine engine, PlayerModel player, BoardSpace propSpace, CancellationToken ct)
    {
        var property = engine.Cache.Game.GetPropertySpace(propSpace.Index);
        if (property == null) return;
        
        var cost = PropertyService.PropertyRent(engine, player, propSpace, property);
        if (cost == 0) return;

        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, $"Rent for {property.Name}",
            $"You owe {RuleDictionary.Currency}{cost} in rent for landing on {property.Name}. This property is in free parking.", ct: ct);
        
        await _transactionService.PayRent(engine, player, cost, property.BoardIndex, ct);
    }
    
    public async Task ProcessFreeParking(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //TODO: grab a free parking card:
        //Then do what the card says; fall back to default logic (game-rules.md)

        //Default Outcomes for Free Parking:
        //- A) No money in FP, OR the player has NO properties: pay fee
        //- B) Has money in FP, and the player has a property they can hand in: take money and FP property, and hand in property
        //- C) Has money in FP, but not properties valid to hand in: purge, take money, and take property from FP
        
        if (engine.Cache.Game.FreeParkingAmount == 0 
            || engine.Cache.Game.GetOwnedProperties(player.PlayerId).Count == 0)
        {
            //A) No money, so pay fee based on dice roll
            var roll = engine.Cache.TurnDiceRoll;
            if(roll?.Die2 == null)
                throw new InvalidOperationException("Cannot process free parking without a valid dice roll");

            var diff = (uint)Math.Abs(roll.Die1 - (ushort)roll.Die2);
            var fee = diff * RuleDictionary.FPPayMultiplier;
            
            engine.CiteRule(RuleCode.FreeParking_PayDiceDifference);
            if(diff == 0)
            {
                //Doubles/triples give no fee
                engine.CiteRule(RuleCode.FreeParking_NoPayOnDouble);
                return;
            }
                
            fee = MoneyHelper.NormaliseAmount(fee, engine.Cache.RoundingRule, FinancialReason.FreeParkingPay);
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Free Parking Fee",
                $"There is no money in free parking, or you do not have any properties to hand into free parking. " +
                $"You will now pay {RuleDictionary.FPPayMultiplier} x the difference between the dice roll: {RuleDictionary.Currency}{fee}.", ct: ct);
            
            await _transactionService.PayIntoFreeParking(engine, player, fee, ct);
            return;
        }
        
        var eligibleProperties = engine.Cache.Game.TradableProperties(player.PlayerId);
        eligibleProperties = eligibleProperties.Where(p =>
        {
            //Get the set for each tradable property; eligible when not null and not already handed in
            var set = PropertySetHelper.ResolveSet(p.BoardIndex);
            return set != null && !player.FPHandedInSets.Contains((PropertySet)set);
        }).ToList();

        //Take from FP (money and any properties)
        await TakeFromFreeParking(engine, player, ct);
        
        if (eligibleProperties.Count == 0)
        {
            //C) Purge a property, take money, and take property from FP (already taken)
            engine.CiteRule(RuleCode.FreeParking_PurgeWhenNoneEligible);
            
            //Purge ONE property
            await _purgingService.PurgeOwnProperty(engine, player, 1, ct);
            
            engine.Cache.Game.CheckReservationRuleSetObtained(player.PlayerId);
            _propertyService.NormaliseProperties(engine);
            return;
        }
        
        //B) Hand in an eligible property
        engine.CiteRule(RuleCode.FreeParking_HandInEligibility);
        engine.CiteRule(RuleCode.FreeParking_HandInTrackedPerSet);
        
        var selected = await engine.PromptProvider.RequestAsync(new TargetPropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = "Hand in a property",
            Body = "Which property would you like to hand in to free parking?",
            EligibleBoardIndexes = eligibleProperties.Select(p => p.BoardIndex).ToList(),
            Count = 1
        }, ct: ct);
        
        if(selected.SelectedBoardIndexes.Count == 0)
            throw new InvalidOperationException("No property selected");
        
        var handInProp = engine.Cache.Game.GetPropertySpace(selected.SelectedBoardIndexes[0]);
        if(handInProp == null)
            throw new InvalidOperationException("Property not found");
        
        var set = PropertySetHelper.ResolveSet(handInProp.BoardIndex);
        if(set == null)
            throw new InvalidOperationException("Property is not a property");
        
        //Hand in the property and record handed in set type:
        _propertyTransferService.HandIntoFreeParking(engine, player, handInProp);
        player.FPHandedInSets.Add((PropertySet)set);
        
        engine.Cache.Game.CheckReservationRuleSetObtained(player.PlayerId);
        _propertyService.NormaliseProperties(engine);
    }


    private async Task TakeFromFreeParking(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var percentCap = engine.Cache.Game.PlayerPercentCap(player.PlayerId);
        var cap = percentCap switch
        {
            100 => RuleDictionary.DHotelFPCap,
            _ => RuleDictionary.NormalFPCap
        };

        uint amount = cap;
        if (engine.Cache.Game.FreeParkingAmount < amount)
            amount = engine.Cache.Game.FreeParkingAmount;
        
        var fpProperties = engine.Cache.Game.Properties
            .Where(p => p.State == PropertyState.FreeParking)
            .ToList();
        
        //Free parking take rules:
        engine.CiteRule(RuleCode.FreeParking_TakeCap);
        engine.CiteRule(RuleCode.FreeParking_TakeProperties);
        
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Take from Free Parking",
            $"You will take {RuleDictionary.Currency}{amount} from free parking, " +
            $"and {fpProperties.Count} property(s) from free parking.", ct: ct);
        
        await _transactionService.TakeFromFreeParking(engine, player, amount, ct);

        foreach (var p in fpProperties)
        {
            _propertyTransferService.TakeFromFreeParking(engine, player, p);
        }
    }
}