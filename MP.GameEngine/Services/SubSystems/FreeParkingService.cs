using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Cards;

namespace MP.GameEngine.Services.SubSystems;

public class FreeParkingService
{
    private readonly TransactionService _transactionService;
    private readonly PropertyTransferService _propertyTransferService;
    private readonly PropertyService _propertyService;
    private readonly PurgingService _purgingService;
    private readonly CardTriggerService _triggerService;

    public FreeParkingService(TransactionService transactionService,
        PropertyTransferService propertyTransferService,
        PropertyService propertyService,
        PurgingService purgingService,
        CardTriggerService triggerService)
    {
        _transactionService = transactionService;
        _propertyTransferService = propertyTransferService;
        _propertyService = propertyService;
        _purgingService = purgingService;
        _triggerService = triggerService;
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
        //Free parking event:
        if (engine.Cache.Game.GlobalEventInfo.RealFreeParking)
        {
            //Free parking is disabled during this event
            engine.CiteRule(RuleCode.Event_FreeParking);
            return;
        }
        
        //Draw + resolve the Free Parking space card FIRST (game-rules.md Free Parking §1 — the drawn
        //card resolves and may supersede the defaults). This MUST precede the held OnLandFreeParking
        //cards: a held "take ALL the Free Parking money" empties the pot, so a pot-reading drawn card
        //(e.g. "every player receives 50% of the Free Parking pot from the bank") has to read the pot
        //BEFORE the held card empties it — otherwise the other players are paid off an emptied pot. Issue #15.
        SuppressDefault? suppressDefault = null;

        //Check if this player should be prevented from drawing a Free Parking card:
        if(!engine.Cache.PreventBoardIndexCard(player.PlayerId, IndexHelper.FreeParkingSpace))
            suppressDefault = await engine.CardService.DrawCard(engine, player, CardType.FreeParking, ct);

        //A drawn card that wipes Free Parking entirely ("all money/property returned to the bank")
        //supersedes everything — nothing left to take, so don't even offer the held take cards.
        if(suppressDefault?.SuppressAllFreeParking == true)
            return;

        //THEN the held OnLandFreeParking cards (take-all / no-cash-this-visit): they take the pot
        //money and/or suppress the default take.
        var triggerSuppress = await _triggerService.OnLandFreeParking(engine, player, ct);

        var sd = new SuppressDefault(triggerSuppress.Type());
        if(suppressDefault is not null)
            sd.Aggregate(suppressDefault);

        if(sd.SuppressAllFreeParking)
            return;
        
        
        //Default Outcomes for Free Parking:
        //- A) No money in FP, OR the player has NO properties: pay fee
        //- B) Has money in FP, and the player has a property they can hand in: take money and FP property, and hand in property
        //- C) Has money in FP, but not properties valid to hand in: purge, take money, and take property from FP
        
        //Dont go down path A if we suppressed money take (meaning we already took money)
        if (!sd.SuppressFreeParkingMoneyTake && (engine.Cache.Game.FreeParkingAmount == 0 
            || engine.Cache.Game.GetOwnedProperties(player.PlayerId).Count == 0))
        {
            if(sd.SuppressFreeParkingFine)
                return;
            
            //A) No money, so pay fee based on dice roll
            var roll = engine.Cache.GetTurnDiceRoll();
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

        //Take from FP (money and any properties) — pass the combined suppress (sd) so a held card's
        //suppression (e.g. "no cash on your next FP visit" → SuppressFreeParkingMoneyTake, supplied via
        //the OnLandFreeParking trigger, not the drawn card) is honoured, not just the drawn card's.
        await TakeFromFreeParking(engine, player, sd, ct);
        
        if (eligibleProperties.Count == 0)
        {
            if(sd.SuppressFreeParkingPurge)
                return;
            
            //C) Purge a property, take money, and take property from FP (already taken)
            engine.CiteRule(RuleCode.FreeParking_PurgeWhenNoneEligible);
            
            //Purge ONE property
            await _purgingService.PurgeOwnProperty(engine, player, 1, ct);
            
            engine.Cache.Game.CheckReservationRuleSetObtained(player.PlayerId);
            _propertyService.NormaliseProperties(engine);
            return;
        }
        
        if(sd.SuppressFreeParkingPropertyHandIn)
            return;
        
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


    private async Task TakeFromFreeParking(Framework.GameEngine engine, PlayerModel player, SuppressDefault suppressDefault, CancellationToken ct)
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
        
        var triggerSuppress = await _triggerService.OnOtherTakesFreeParking(engine, player, amount, ct);
        suppressDefault.Aggregate(triggerSuppress);
        
        var fpProperties = engine.Cache.Game.Properties
            .Where(p => p.State == PropertyState.FreeParking)
            .ToList();

        var body = "You will take ";
        body += !suppressDefault.SuppressFreeParkingMoneyTake
            ? $"{RuleDictionary.Currency}{amount} from free parking"
            : string.Empty;
        body += !suppressDefault.SuppressFreeParkingMoneyTake && !suppressDefault.SuppressFreeParkingPropertyTake
            ? ", and "
            : string.Empty;
        body += !suppressDefault.SuppressFreeParkingPropertyTake
            ? $"{fpProperties.Count} property(s) from free parking"
            : string.Empty;

        if (suppressDefault is { SuppressFreeParkingPropertyTake: true, SuppressFreeParkingMoneyTake: true })
            return;
        
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Take from Free Parking", body, ct: ct);
        
        if(!suppressDefault.SuppressFreeParkingMoneyTake)
        {
            engine.CiteRule(RuleCode.FreeParking_TakeCap);
            await _transactionService.TakeFromFreeParking(engine, player, amount, ct);
        }

        if(!suppressDefault.SuppressFreeParkingPropertyTake)
        {
            engine.CiteRule(RuleCode.FreeParking_TakeProperties);
            foreach (var p in fpProperties)
            {
                _propertyTransferService.TakeFromFreeParking(engine, player, p);
            }
        }
    }
}