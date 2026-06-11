using JC.Core.Extensions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class PropertyService
{
    private readonly AuctionService _auctionService;
    private readonly TransactionService _transactionService;
    private readonly PropertyTransferService _propertyTransferService;

    public PropertyService(AuctionService auctionService,
        TransactionService transactionService,
        PropertyTransferService propertyTransferService)
    {
        _auctionService = auctionService;
        _transactionService = transactionService;
        _propertyTransferService = propertyTransferService;
    }

    public List<PropertyModel> GetProperties(Board board)
        => board.Spaces
            .Where(s => s.PropertySet != null)
            .Select(s => new PropertyModel
            {
                Name = s.Name,
                BoardIndex = s.Index,
                
                //Explicit defaults:
                OwnerPlayerId = null,
                State = PropertyState.NotOwned,
                RentLevel = RentLevel.SINGLE,
                StreetRuleQualifier = StreetRuleQualifier.None
            }).ToList();

    public async Task ProcessUnownedProperty(Framework.GameEngine engine, PlayerModel player, 
        BoardSpace space, PropertyModel property, CancellationToken ct)
    {
        //Process Unowned Property Paths:
        //A) RESERVATION: Sends prompt to either RESERVE OR IGNORE
        //B) AFFORD: Sends prompt to BUY or to AUCTION
        //C) SHORTFALL: Sends acknowledge prompt that AUCTION will begin

        if(!space.IsPurchasable || !space.Index.IsProperty(false))
            //No-op - space cannot be purchased or not a property
            return;
        
        if(property.State != PropertyState.NotOwned)
            //No-op - property already owned
            return;
        
        var ownedInSet = engine.Cache.Game.GetOwnedProperties(player.PlayerId, space.PropertySet);
        
        //PropSet cannot be null (or shouldn't, since it is a property and purchasable)
        var buyingLastInSet = PropertySetHelper.MustReserve((PropertySet)space.PropertySet!, ownedInSet);
        if (buyingLastInSet)
        {
            //This will turn off the reservation rule if everyone else has a reservation
            engine.Cache.Game.CheckReservationRule(player.PlayerId);
            if(!engine.Cache.Game.ReserveRuleActive)
                //Cite rule that its turned off:
                engine.CiteRule(RuleCode.Reserved_MechanicEnds);
        }
        
        //Reserve route needs BOTH the rule active AND this being the player's
        //set-completing property — a non-completer is a normal buy/auction even
        //during the reserve phase. The deadlock check above may also have just
        //turned the rule off (everyone else already reserved), in which case this
        //set-completer falls through to an outright buy.
        if(buyingLastInSet && engine.Cache.Game.ReserveRuleActive)
        {
            //Cite reserved rules:
            engine.CiteRule(RuleCode.Reserved_NoSetUntilAllCan);
            engine.CiteRule(RuleCode.Reserved_ReserveFinalProperty);
            
            //Reservation route (A)
            await ReserveProperty(engine, player, space, property, ct);
        }
        else
            //Normal unowned route (B or C)
            await UnownedProperty(engine, player, space, property, ct);
    }

    private async Task ReserveProperty(Framework.GameEngine engine, PlayerModel player,
        BoardSpace space, PropertyModel property, CancellationToken ct)
    {
        if(space.PurchaseCost is null or 0)
            throw new InvalidOperationException("Purchase cost cannot be null or 0");

        var cost = MoneyHelper.ReservePrice(property.BoardIndex, engine.Cache.Board, engine.Cache.RoundingRule);
        if(cost == 0) throw new InvalidOperationException("Reserve price cannot be 0");

        if (player.Money < cost)
        {
            //Cannot reserve, therefore a no-op, but inform player
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Reserve",
                $"You do not have enough money to reserve {property.Name} for {RuleDictionary.Currency}{cost}.", ct: ct);
            return;
        }
        
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Reserve {property.Name}",
            Body = $"Would you like to reserve {property.Name} for {RuleDictionary.Currency}{cost}?",
            BoardIndex = space.Index,
            Cost = cost,
            Type = AcquirePropertyType.Reserve
        }, ct: ct);
        
        if(!response.Accept)
            //Ignoring, no auction when declining to reserve
            return;

        //Uses same transaction method for purchase (still purchasing the property, just at reservation price)
        await _transactionService.PurchaseProperty(engine, player, cost, space.Index, ct);
        _propertyTransferService.Reserve(engine, player, property);
        
        NormaliseProperties(engine);
    }


    public uint GetPropertyCost(Framework.GameEngine engine, PlayerModel player,
        BoardSpace space, PropertyModel property)
    {
        if(space.PurchaseCost is null or 0)
            //Throws because this space IS a purchasable property, and MUST have a purchase cost
            throw new InvalidOperationException("Purchase cost cannot be null or 0");

        var cost = (uint)space.PurchaseCost;
        if (property.BoardIndex.IsStation())
        {
            //Owned stations includes mortgaged stations as the player still controls those stations
            //Since those stations are in control of the player, the ownership increase per station applies
            var ownedStationsCount = engine.Cache.Game.GetOwnedProperties(player.PlayerId, PropertySet.Station).Count;
            cost = ownedStationsCount switch
            {
                0 => RuleDictionary.SingleStationCost,
                1 => RuleDictionary.SecondStationCost,
                2 => RuleDictionary.ThirdStationCost,
                3 => RuleDictionary.FourthStationCost,
                _ => throw new InvalidOperationException("Invalid number of owned stations")
            };
            
            //Cite rules:
            engine.CiteRule(RuleCode.Station_PriceScales);
            engine.CiteRule(RuleCode.Station_MortgagedCountsForPrice);
        }
        
        return MoneyHelper.NormaliseAmount(cost, engine.Cache.RoundingRule, FinancialReason.Purchase);
    }
    
    
    private async Task UnownedProperty(Framework.GameEngine engine, PlayerModel player,
        BoardSpace space, PropertyModel property, CancellationToken ct)
    {
        var cost = GetPropertyCost(engine, player, space, property);
        var canAfford = player.Money >= cost;
        
        var runAuction = false;
        if (canAfford)
        {
            //Player CAN afford, therefore ask if they want to buy or auction
            var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
            {
                PlayerId = player.PlayerId,
                Title = $"Purchase {property.Name}",
                Body = $"Would you like to purchase {property.Name} for {RuleDictionary.Currency}{cost}, or auction it?",
                BoardIndex = property.BoardIndex,
                Cost = cost,
                Type = AcquirePropertyType.Buy
            }, ct: ct);

            //If they do not accept to buy, auction will be held
            if (!response.Accept)
                runAuction = true;
        }
        else
        {
            //Player cannot afford, therefore auction will be held
            runAuction = true;
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, $"You Cannot Afford {property.Name}",
                $"You do not have enough money to purchase {property.Name} for {RuleDictionary.Currency}{cost}. " +
                "An auction will be held.", ct: ct);
        }
        
        var owningPlayer = player;
        if (runAuction)
        {
            //Cite rule:
            engine.CiteRule(RuleCode.Auction_Trigger);
            
            //Auction will be held, therefore run the auction
            var outcome = await _auctionService.RunAuction(engine, player.PlayerId, property.BoardIndex, ct);
            if(!outcome.Success || outcome.Winner is null)
                //Auction cancelled/failed, therefore a no-op
                return;
                
            //Charge the winning player
            owningPlayer = outcome.Winner;
            await _transactionService.WinAuction(engine, owningPlayer, outcome.Price, property.BoardIndex, ct);
            _propertyTransferService.WinAtAuction(engine, owningPlayer, property);
        }
        else
        {
            //No auction, therefore a purchase
            await _transactionService.PurchaseProperty(engine, owningPlayer, cost, property.BoardIndex, ct);
            _propertyTransferService.Buy(engine, owningPlayer, property);
        }
        
        //Old:
        // property.OwnProperty(owningPlayer.PlayerId);
        
        
        //An auction win can complete a set for the winner even while the reserve
        //rule is active (the lander declined a property that didn't complete *their*
        //set). A player breaking through to a full set ends the mechanic for everyone
        //— game-rules.md Reserved Properties; auction-flow.md §7. No-op on the plain
        //buy path (a set-completing buy is handled by the reserve route upstream).
        engine.Cache.Game.CheckReservationRuleSetObtained(owningPlayer.PlayerId);
        NormaliseProperties(engine);
    }


    public async Task PayPropertyRent(Framework.GameEngine engine, PlayerModel player, 
        BoardSpace space, PropertyModel property, CancellationToken ct)
    {
        var cost = PropertyRent(engine, player, space, property);
        if (cost == 0) return;
        
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, $"Rent for {property.Name}",
            $"You owe {RuleDictionary.Currency}{cost} in rent for landing on {property.Name}.", ct: ct);
        
        await _transactionService.PayRent(engine, player, cost, property.BoardIndex, ct);
    }

    public static uint PropertyRent(Framework.GameEngine engine, PlayerModel player, BoardSpace space, PropertyModel property)
    {
        if(!space.IsRentable || !property.ChargeRent(player.PlayerId))
        {
            if(property.State == PropertyState.Reserved)
                engine.CiteRule(RuleCode.Reserved_PropertyInert);
            
            //Not rentable or the player owns this property, therefore a no-op
            return 0;
        }

        var rent = space.GetRent(property.RentLevel);
        if(rent == null) throw new InvalidOperationException("Rent cannot be null for rentable space");

        if (space.PropertySet == PropertySet.Utility)
        {
            rent = (ushort)(engine.Cache.Game.Metadata.CurrentPlayerId == player.PlayerId
                //Is the player paying rent the turn roller (their turn)?
                //If so, utility multiplier multiplied by (die1 + die2); otherwise by third die
                ? (ushort)rent * (engine.Cache.TurnDiceRoll?.Die1 + engine.Cache.TurnDiceRoll?.Die2 ?? 0) //Should not be null, defensive
                : (ushort)rent * (engine.Cache.TurnDiceRoll?.ThirdDie ?? 0)); //Should not be null, defensive
            
            //Cite utility rent rules:
            engine.CiteRule(RuleCode.Utility_RentIsDiceTimesMultiplier);
            engine.CiteRule(RuleCode.Utility_DiceDependsOnArrival);
            engine.CiteRule(RuleCode.Utility_PairMultiplier);
        }

        if (property.State != PropertyState.FreeParking && property.OwnerPlayerId != null)
        {
            var owner = engine.Cache.Game.GetPlayer(property.OwnerPlayerId);
            if (owner == null)
                throw new InvalidOperationException("Owner player cannot be null");

            if (owner.IsInJail)
            {
                engine.CiteRule(RuleCode.Default_NoRentWhileOwnerJailed);
                return 0;
            }
        }
        
        var cost = MoneyHelper.NormaliseAmount((uint)rent, engine.Cache.RoundingRule, FinancialReason.Rent);
        return cost;
    }


    public void NormaliseProperties(Framework.GameEngine engine)
    {
        //This normalises the rent levels for all properties in the game
        var propList = engine.Cache.Board.GetPropertySpaces(engine.Cache.Game.Properties);

        foreach (var (propModel, propSpace) in propList)
        {
            if (propModel.State is PropertyState.NotOwned or PropertyState.FreeParking 
                    or PropertyState.Reserved or PropertyState.Mortgaged 
                || string.IsNullOrEmpty(propModel.OwnerPlayerId))
            {
                //Unowned, FP, reserved, and mortgaged properties are always single-rented
                propModel.RentLevel = RentLevel.SINGLE;
                continue;
            }
                                                                                                                                                       
            var set = propSpace.PropertySet ?? throw new InvalidOperationException("Property space has no property set");  
            
            //Count the number of owned properties in the set (by current property owner), excluding mortgaged and reserved properties
            var ownedInSet = engine.Cache.Game.GetOwnedProperties(propModel.OwnerPlayerId!, set, 
                includeMortgaged: false, includeReserved: false);                                                                                  
                                                                                                                                                                                                                                             
            if (set == PropertySet.Station)
                //Stations need their own, since they have unique rent levels
                propModel.RentLevel = ownedInSet.Count switch 
                    { 
                        1 => RentLevel.SINGLE, 
                        2 => RentLevel.DOUBLE, 
                        3 => RentLevel.TRIPLE, 
                        4 => RentLevel.SET, 
                        _ => throw new InvalidOperationException($"Invalid owned count for station: {ownedInSet.Count}") 
                    };                                                                                                                              
            else if (!propModel.BuiltOn())
                //Built on is ignored, rent level for built on properties is never normalised - only modified when buy/sell houses
                //Works for both buildable properties and utilities (only 2 ulitilies)
                propModel.RentLevel = ownedInSet.Count == PropertySetHelper.GetIndexes(set).Count
                    ? RentLevel.SET
                    : RentLevel.SINGLE;
            else
            {
                //Property has been built on, so check purged state
                var rentLevels = ownedInSet.Select(p => p.RentLevel).ToList();
                if (rentLevels.All(l => propModel.RentLevel >= l - 1 && propModel.RentLevel <= l + 1))
                    propModel.IsPurged = false;
            }
            
            if(propModel.StreetRuleQualifier == StreetRuleQualifier.BuiltOn)
                continue;
            
            if(set is PropertySet.Utility or PropertySet.Station)
                continue;
            
            //Street qualification includes reserved properties:
            var streetSet = PropertySetHelper.StreetPartner[set];
            ownedInSet = engine.Cache.Game.GetOwnedProperties(propModel.OwnerPlayerId!, set, includeMortgaged: false);
            var ownedInMatchSet = engine.Cache.Game.GetOwnedProperties(propModel.OwnerPlayerId!, streetSet, includeMortgaged: false);

            if (ownedInSet.Count != PropertySetHelper.GetIndexes(set).Count
                || ownedInMatchSet.Count != PropertySetHelper.GetIndexes(streetSet).Count)
            {
                propModel.StreetRuleQualifier = propModel.StreetRuleQualifier switch
                {
                    StreetRuleQualifier.BuiltOn => StreetRuleQualifier.BuiltOn,
                    StreetRuleQualifier.Qualified => propModel.RentLevel is > RentLevel.SET and <= RentLevel.DOUBLE_HOTEL 
                        ? StreetRuleQualifier.BuiltOn
                        : StreetRuleQualifier.NeverBuiltOn,
                    _ => StreetRuleQualifier.NeverBuiltOn
                };
                continue;
            }
                
            //All properties in both sets have never been built on (set to this upon ownership),
            //Or are already qualified (set to qualified in a previous loop step).
            //Therefore, this property also qualifies
            if(ownedInSet.All(p => p.StreetRuleQualifier is StreetRuleQualifier.NeverBuiltOn or StreetRuleQualifier.Qualified) 
               && ownedInMatchSet.All(p => p.StreetRuleQualifier is StreetRuleQualifier.NeverBuiltOn or StreetRuleQualifier.Qualified))
                propModel.StreetRuleQualifier = StreetRuleQualifier.Qualified;
        }
    }
}