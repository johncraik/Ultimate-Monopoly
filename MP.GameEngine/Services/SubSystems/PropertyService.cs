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

    public PropertyService(AuctionService auctionService,
        TransactionService transactionService)
    {
        _auctionService = auctionService;
        _transactionService = transactionService;
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
            //This will turn off the reservation rule if everyone else has a reservation
            engine.Cache.Game.CheckReservationRule(player.PlayerId);
        
        //Reserve route needs BOTH the rule active AND this being the player's
        //set-completing property — a non-completer is a normal buy/auction even
        //during the reserve phase. The deadlock check above may also have just
        //turned the rule off (everyone else already reserved), in which case this
        //set-completer falls through to an outright buy.
        if(buyingLastInSet && engine.Cache.Game.ReserveRuleActive)
            //Reservation route (A)
            await ReserveProperty(engine, player, space, property, ct);
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
        
        //We first OWN the property, then RESERVE it
        property.OwnProperty(player.PlayerId);
        property.ReserveProperty();
        NormaliseRentLevels(engine);
    }


    private async Task<uint> GetPropertyCost(Framework.GameEngine engine, PlayerModel player,
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
        }
        
        return MoneyHelper.NormaliseAmount(cost, engine.Cache.RoundingRule, FinancialReason.Purchase);
    }
    
    
    private async Task UnownedProperty(Framework.GameEngine engine, PlayerModel player,
        BoardSpace space, PropertyModel property, CancellationToken ct)
    {
        var cost = await GetPropertyCost(engine, player, space, property);
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
            //Auction will be held, therefore run the auction
            var outcome = await _auctionService.RunAuction(engine, property.BoardIndex, ct);
            if(!outcome.Success || outcome.Winner is null)
                //Auction cancelled/failed, therefore a no-op
                return;
                
            //Charge the winning player
            owningPlayer = outcome.Winner;
            await _transactionService.WinAuction(engine, owningPlayer, outcome.Price, property.BoardIndex, ct);
        }
        else
            //No auction, therefore a purchase
            await _transactionService.PurchaseProperty(engine, owningPlayer, cost, property.BoardIndex, ct);
        
        property.OwnProperty(owningPlayer.PlayerId);
        //An auction win can complete a set for the winner even while the reserve
        //rule is active (the lander declined a property that didn't complete *their*
        //set). A player breaking through to a full set ends the mechanic for everyone
        //— game-rules.md Reserved Properties; auction-flow.md §7. No-op on the plain
        //buy path (a set-completing buy is handled by the reserve route upstream).
        engine.Cache.Game.CheckReservationRuleSetObtained(owningPlayer.PlayerId);
        NormaliseRentLevels(engine);
    }


    public async Task PayPropertyRent(Framework.GameEngine engine, PlayerModel player, 
        BoardSpace space, PropertyModel property, CancellationToken ct)
    {
        if(!space.IsRentable || !property.ChargeRent(player.PlayerId))
            //Not rentable or the player owns this property, therefore a no-op
            return;

        var rent = space.GetRent(property.RentLevel);
        if(rent == null) throw new InvalidOperationException("Rent cannot be null for rentable space");

        if (space.PropertySet == PropertySet.Utility)
            rent = (ushort)(engine.Cache.Game.Metadata.CurrentPlayerId == player.PlayerId
                //Is the player paying rent the turn roller (their turn)?
                //If so, utility multiplier multiplied by (die1 + die2); otherwise by third die
                ? (ushort)rent * (engine.Cache.TurnDiceRoll?.Die1 + engine.Cache.TurnDiceRoll?.Die2 ?? 0) //Should not be null, defensive
                : (ushort)rent * (engine.Cache.TurnDiceRoll?.ThirdDie ?? 0)); //Should not be null, defensive
        
        var cost = MoneyHelper.NormaliseAmount((uint)rent, engine.Cache.RoundingRule, FinancialReason.Rent);
        if(cost == 0)
            //No rent, therefore a no-op
            return;
        
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, $"Rent for {property.Name}",
            $"You owe {RuleDictionary.Currency}{cost} in rent for landing on {property.Name}.", ct: ct);
        
        await _transactionService.PayRent(engine, player, (uint)rent, property.BoardIndex, ct);
    }


    public void NormaliseRentLevels(Framework.GameEngine engine)
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
            var owned = engine.Cache.Game.GetOwnedProperties(propModel.OwnerPlayerId!, set, 
                includeMortgaged: false, includeReserved: false).Count;                                                                                  
                                                                                                                                                                                                                                             
            if (set == PropertySet.Station)
                //Stations need their own, since they have unique rent levels
                propModel.RentLevel = owned switch 
                    { 
                        1 => RentLevel.SINGLE, 
                        2 => RentLevel.DOUBLE, 
                        3 => RentLevel.TRIPLE, 
                        4 => RentLevel.SET, 
                        _ => throw new InvalidOperationException($"Invalid owned count for station: {owned}") 
                    };                                                                                                                              
            else if (!propModel.BuiltOn()) 
                //Built on is ignored, rent level for built on properties is never normalised - only modified when buy/sell houses 
                //Works for both buildable properties and utilities (only 2 ulitilies)
                propModel.RentLevel = owned == PropertySetHelper.GetIndexes(set).Count 
                    ? RentLevel.SET 
                    : RentLevel.SINGLE;
        }
    }



    #region Player Command Actions

    private async Task<(bool Success, PlayerModel? Player, BoardSpace? Space, PropertyModel? Property)> 
        PropertyActionValidation(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
    {
        //Get and check the current player:
        var player = engine.Cache.Game.CurrentPlayer();
        if (player is null)
            return (false, null, null, null);
        
        //Get and check the board space:
        var space = engine.Cache.Board.GetBoardSpace(boardIndex);
        if (space.PropertySet is null)
            //Not a property, therefore a no-op
            return (false, player, null, null);
        
        //Get and check the property linked to the board space:
        var property = engine.Cache.Game.GetPropertySpace(boardIndex);
        if (property is null)
            return (false, player, space, null);

        //Return success if player owns property
        return (property.OwnerPlayerId == player.PlayerId, player, space, property);
    }
    
    
    public async Task TryUnReserveProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
    {
        //Cannot unreserve if the reserve rule is active (everyone else must reserve)
        if (engine.Cache.Game.ReserveRuleActive)
            return;

        //Validate
        var (success, player, space, property) = await PropertyActionValidation(engine, boardIndex, ct);
        if (!success || player is null || space is null || property is null)
            return;

        //Check property IS reserved, return if not
        if (property.State != PropertyState.Reserved)
            return;
        
        var cost = await GetPropertyCost(engine, player, space, property);
        if (player.Money < cost)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Un-Reserve",
                $"You do not have enough money to un-reserve {property.Name}.", ct: ct);
            return;
        }
        
        //Ask to confirm they want to unreserve the property:
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Un-Reserve {property.Name}",
            Body = $"Would you like to un-reserve {property.Name} for {RuleDictionary.Currency}{cost}?",
            BoardIndex = property.BoardIndex,
            Cost = cost,
            Type = AcquirePropertyType.UnReserve
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        await _transactionService.UnReserveProperty(engine, player, cost, property.BoardIndex, ct);
        
        property.UnreserveProperty();
        NormaliseRentLevels(engine);
    }

    public async Task TryMortgageProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
    {
        //Validate
        var (success, player, space, property) = await PropertyActionValidation(engine, boardIndex, ct);
        if (!success || player is null || space is null || property is null)
            return;
        
        //Ensures property is owned
        if(property.State != PropertyState.Owned)
            return;
        
        var canMortgage = engine.Cache.Game.CanMortgageProperty(player.PlayerId, property.BoardIndex);
        if(!canMortgage)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Mortgage",
                $"You cannot mortgage {property.Name} at this time.", ct: ct);
            return;
        }
        
        var mortgageValue = MoneyHelper.MortgageValue(property.BoardIndex, engine.Cache.Board, engine.Cache.RoundingRule);
        //Ask to confirm they want to unmortgage the property:
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Mortgage {property.Name}",
            Body = $"Would you like to mortgage {property.Name} and receive {RuleDictionary.Currency}{mortgageValue}?",
            BoardIndex = property.BoardIndex,
            Cost = mortgageValue,
            Type = AcquirePropertyType.Mortgage
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        await _transactionService.ReceiveForMortgage(engine, player, mortgageValue, property.BoardIndex, ct);
        
        property.MortgageProperty();
        NormaliseRentLevels(engine);
    }
    
    public async Task TryUnmortgageProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
    {
        //Validate
        var (success, player, space, property) = await PropertyActionValidation(engine, boardIndex, ct);
        if (!success || player is null || space is null || property is null)
            return;

        //Check property IS mortgaged, return if not
        if (property.State != PropertyState.Mortgaged)
            return;
        
        var cost = MoneyHelper.UnMortgageCost(property.BoardIndex, engine.Cache.Board, engine.Cache.RoundingRule);
        if (player.Money < cost)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Unmortgage",
                $"You do not have enough money to unmortgage {property.Name}.", ct: ct);
            return;
        }
        
        //Ask to confirm they want to un-mortgage the property:
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Unmortgage {property.Name}",
            Body = $"Would you like to unmortgage {property.Name} for {RuleDictionary.Currency}{cost}?",
            BoardIndex = property.BoardIndex,
            Cost = cost,
            Type = AcquirePropertyType.UnMortgage
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        await _transactionService.PayToUnmortgage(engine, player, cost, property.BoardIndex, ct);
        
        property.UnmortgageProperty();
        NormaliseRentLevels(engine);
    }

    
    public async Task<bool> TryBuildOnProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
        => await TryBuildOnProperties(engine, [boardIndex], ct);

    public async Task<bool> TryBuildOnProperties(Framework.GameEngine engine, PropertySet set, CancellationToken ct)
    {
        if (set is PropertySet.Utility or PropertySet.Station)
            return false;
        
        var indexes = PropertySetHelper.GetIndexes(set);
        return await TryBuildOnProperties(engine, indexes, ct);
    }
    
    
    public async Task<bool> TryBuildOnProperties(Framework.GameEngine engine, List<ushort> boardIndexes, CancellationToken ct)
    {
        if(boardIndexes.Any(i => !i.IsProperty()))
            //Any non-buildable properties and return false (cannot build)
            return false;
        
        //TODO - Will add house to each property in list
        //If any properties unowned by current player - it throws
        //If any other validation errors, it no-ops
        return true;
    }
    
    
    public async Task<bool> TrySellOnProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
        => await TrySellOnProperties(engine, [boardIndex], ct);

    public async Task<bool> TrySellOnProperties(Framework.GameEngine engine, PropertySet set, CancellationToken ct)
    {
        if (set is PropertySet.Utility or PropertySet.Station)
            return false;
        
        var indexes = PropertySetHelper.GetIndexes(set);
        return await TrySellOnProperties(engine, indexes, ct);
    }
    
    
    public async Task<bool> TrySellOnProperties(Framework.GameEngine engine, List<ushort> boardIndexes, CancellationToken ct)
    {
        if(boardIndexes.Any(i => !i.IsProperty()))
            //Any non-buildable properties and return false (cannot sell house)
            return false;
        
        //TODO - Will remove house from each property in list
        //If any properties unowned by current player - it throws
        //If any other validation errors, it no-ops
        return true;
    }
    

    #endregion
}