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
            
            //Cite rules:
            engine.CiteRule(RuleCode.Station_PriceScales);
            engine.CiteRule(RuleCode.Station_MortgagedCountsForPrice);
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
        NormaliseRentLevels(engine);
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



    #region Mortgage/Unmortgage/Unreserve

    private (bool Success, PlayerModel? Player, BoardSpace? Space, PropertyModel? Property) 
        PropertyActionValidation(Framework.GameEngine engine, ushort boardIndex, string? playerId = null)
    {
        //Get and check the current player:
        var player = engine.Cache.Game.GetPlayer(playerId ?? engine.Cache.Game.Metadata.CurrentPlayerId);
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
    
    
    public async Task UnReserveProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
    {
        //Cannot unreserve if the reserve rule is active (everyone else must reserve)
        if (engine.Cache.Game.ReserveRuleActive)
            return;

        //Validate
        var (success, player, space, property) = PropertyActionValidation(engine, boardIndex);
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

    public async Task MortgageProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct, string? playerId = null)
    {
        //Validate
        var (success, player, space, property) = PropertyActionValidation(engine, boardIndex, playerId);
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
        engine.CiteRule(RuleCode.Mortgage_NoSetRentWhileMortgaged);
        NormaliseRentLevels(engine);
    }


    public async Task PayMortgageFee(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var properties = engine.Cache.Game.GetOwnedProperties(player.PlayerId, includeReserved: false);
        if (properties.Count == 0)
            return;
        
        properties = properties.Where(p => p.State == PropertyState.Mortgaged).ToList();
        if (properties.Count == 0)
            return;
        
        engine.CiteRule(RuleCode.Mortgage_FeeOnGo);
        
        //Sum the per-property mortgage fee (20% of purchase cost, grid-rounded per property — Mortgaging rule 1)
        var fee = properties.Aggregate<PropertyModel, uint>(0,
            (current, p) => current + MoneyHelper.MortgageFee(p.BoardIndex, engine.Cache.Board, engine.Cache.RoundingRule));
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Mortgage Fee",
            $"You are being charged a mortgage fee of {RuleDictionary.Currency}{fee} (total) for {properties.Count} mortgaged properties.", ct: ct);
        
        await _transactionService.PayMortgageFee(engine, player, fee, ct);
    }
    
    
    public async Task UnmortgageProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
    {
        //Validate
        var (success, player, space, property) = PropertyActionValidation(engine, boardIndex);
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

    #endregion



    #region Build On Properties

    public async Task BuildOnProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct)
    {
        //Single property build
        var canBuild = engine.Cache.Game.CanIncreaseRentLevel(boardIndex);
        if(!canBuild)
            return;
        
        var player = engine.Cache.Game.CurrentPlayer();
        if (player is null)
            return;
        
        var space = engine.Cache.Board.GetBoardSpace(boardIndex);
        var property = engine.Cache.Game.GetPropertySpace(boardIndex);
        if(property is null)
            return;
        
        var cost = PropertySetHelper.GetBuildCost(boardIndex, engine.Cache.Board);
        uint? doubleHotelCost = null;
        if (property.RentLevel == RentLevel.HOTEL)
        {
            //Property has a hotel, and we have passed increase rent level check
            //Therefore, we must be building a double hotel (build cost * 5)
            doubleHotelCost = PropertySetHelper.GetDoubleHotelCost(boardIndex, engine.Cache.Board);
            cost = (uint)doubleHotelCost;
        }
        
        cost = MoneyHelper.NormaliseAmountToPositive(cost, engine.Cache.RoundingRule, FinancialReason.Build);        
        if (player.Money < cost)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Build",
                $"You do not have enough money to build on {space.Name}.", ct: ct);
            return;
        }
        
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Build on {space.Name}?",
            Body = $"Are you sure you want to spend {RuleDictionary.Currency}{cost} to build on {space.Name}?",
            Cost = cost,
            BoardIndex = boardIndex,
            Type = AcquirePropertyType.Build
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        await BuildOnProperties(engine, [boardIndex], ct, doubleHotelCost);
    } 

    public async Task BuildOnProperties(Framework.GameEngine engine, PropertySet set, CancellationToken ct)
    {
        //Complete set property build
        
        //Check the set is not utility or station (early return)
        if (set is PropertySet.Utility or PropertySet.Station)
            return;
        
        //Check player can build on all properties in this set
        var canBuild = engine.Cache.Game.CanIncreaseRentLevelForAllInSet(set);
        if(!canBuild)
            return;
        
        var player = engine.Cache.Game.CurrentPlayer();
        if (player is null)
            return;
        
        var index = PropertySetHelper.GetIndexes(set)[0];
        var space = engine.Cache.Board.GetBoardSpace(index);
        
        var cost = PropertySetHelper.GetBuildCost(set, engine.Cache.Board);
        cost = MoneyHelper.NormaliseAmountToPositive(cost, engine.Cache.RoundingRule, FinancialReason.Build);
        
        if (player.Money < cost)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Build",
                $"You do not have enough money to build on all the properties in the {set.ToDisplayName()} set.", ct: ct);
            return;
        }
        
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Build on {space.Name}?",
            Body = $"Are you sure you want to spend {RuleDictionary.Currency}{cost} to build on all properties in the {set.ToDisplayName()} set?",
            Cost = cost,
            BoardIndex = index,
            Type = AcquirePropertyType.BuildAllInSet
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        var indexes = PropertySetHelper.GetIndexes(set);
        await BuildOnProperties(engine, indexes, ct);
    }

    public async Task BuildOnAllProperties(Framework.GameEngine engine, CancellationToken ct)
    {
        //Build on all properties in every set
        
        //Check the player can build on all properties in every set
        var canBuild = engine.Cache.Game.CanIncreaseRentLevelForAll();
        if(!canBuild)
            return;
        
        //Get the current player
        var player = engine.Cache.Game.CurrentPlayer();
        if (player is null)
            return;
        
        //Get list of owned properties (excluding mortgaged and reserved)
        var props = engine.Cache.Game.GetOwnedProperties(includeMortgaged: false, includeReserved: false);
        if (props.Count == 0)
            return;
        
        //Translate the owned properties into a list of sets where they own the complete set:
        var ownedSetsProperties = PropertySetHelper.GetOwnedSets(player.PlayerId, props);
        
        //Get the indexes of every property in the owned sets
        uint cost = 0;
        var indexes = new List<ushort>();
        foreach (var set in ownedSetsProperties)
        {
            var iList = PropertySetHelper.GetIndexes(set);
            indexes.AddRange(iList);
            
            cost += PropertySetHelper.GetBuildCost(set, engine.Cache.Board);
        }
        
        cost = MoneyHelper.NormaliseAmountToPositive(cost, engine.Cache.RoundingRule, FinancialReason.Build);
        
        if (player.Money < cost)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Build",
                $"You do not have enough money to build on all the properties in a set.", ct: ct);
            return;
        }
        
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Build on all properties in a set?",
            Body = $"Are you sure you want to spend {RuleDictionary.Currency}{cost} to build on all properties in a set?",
            Cost = cost,
            BoardIndex = 0,
            Type = AcquirePropertyType.BuildAll
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        //Build on all properties in index list
        await BuildOnProperties(engine, indexes, ct);
    }
    
    
    private async Task BuildOnProperties(Framework.GameEngine engine, List<ushort> boardIndexes, CancellationToken ct,
        uint? doubleHotelCost = null)
    {
        if(boardIndexes.Any(i => !i.IsProperty()))
            //Any non-buildable properties and return false (cannot build)
            return;

        var player = engine.Cache.Game.CurrentPlayer();
        if (player is null)
            return;
        
        List<PropertyModel> properties = boardIndexes.Select(i => engine.Cache.Game.GetPropertySpace(i)).ToList()!;
        if(properties.Any(p => p == null!))
            return;
        
        foreach (var property in properties)
        {
            if(property.State != PropertyState.Owned)
                //Should never throw since validation checks have been done before calling this
                throw new InvalidOperationException("Property is not owned");

            switch (property.RentLevel)
            {
                //Should never throw since validation checks have been done before calling this
                case >= RentLevel.DOUBLE_HOTEL:
                    throw new InvalidOperationException("Cannot build on property with rent level higher than double hotel");
                //Should never throw since validation checks have been done before calling this
                case < RentLevel.SET:
                    throw new InvalidOperationException("Cannot build on property with rent level less than set");
            }
            
            property.RentLevel += 1;
            property.HasBeenBuiltOnThisTurn = true;
            
            var cost = PropertySetHelper.GetBuildCost(property.BoardIndex, engine.Cache.Board);
            await _transactionService.PayForBuild(engine, player, doubleHotelCost ?? cost, property.BoardIndex, ct);
        }
    }

    #endregion


    #region Sell On Properties

    public async Task SellOnProperty(Framework.GameEngine engine, ushort boardIndex, CancellationToken ct, string? playerId = null)
    {
        //Single property sell
        var canSell = engine.Cache.Game.CanDecreaseRentLevel(boardIndex);
        if(!canSell)
            return;
        
        var player = engine.Cache.Game.GetPlayer(playerId ?? engine.Cache.Game.Metadata.CurrentPlayerId);
        if (player is null)
            return;
        
        var space = engine.Cache.Board.GetBoardSpace(boardIndex);
        var property = engine.Cache.Game.GetPropertySpace(boardIndex);
        if(property is null)
            return;
        
        var value = PropertySetHelper.GetSellValue(boardIndex, engine.Cache.Board);
        uint? doubleHotelValue = null;
        if (property.RentLevel == RentLevel.HOTEL)
        {
            //Property has a double hotel, and we have passed decreased rent level check
            //Therefore, we must be selling a double hotel (build cost * 5)/2
            doubleHotelValue = PropertySetHelper.GetDoubleHotelSellValue(boardIndex, engine.Cache.Board);
            value = (uint)doubleHotelValue;
        }
        
        value = MoneyHelper.NormaliseAmountToPositive(value, engine.Cache.RoundingRule, FinancialReason.Sell);        
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Sell on {space.Name}?",
            Body = $"Are you sure you want to sell on {space.Name} and receive {RuleDictionary.Currency}{value}?",
            Cost = value,
            BoardIndex = boardIndex,
            Type = AcquirePropertyType.Sell
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        await SellOnProperties(engine, [boardIndex], ct, doubleHotelValue);
    } 

    public async Task SellOnProperties(Framework.GameEngine engine, PropertySet set, CancellationToken ct)
    {
        //Complete set property sell
        
        //Check the set is not utility or station (early return)
        if (set is PropertySet.Utility or PropertySet.Station)
            return;
        
        //Check player can build on all properties in this set
        var canSell = engine.Cache.Game.CanDecreaseRentLevelForAllInSet(set);
        if(!canSell)
            return;
        
        var player = engine.Cache.Game.CurrentPlayer();
        if (player is null)
            return;
        
        var index = PropertySetHelper.GetIndexes(set)[0];
        var space = engine.Cache.Board.GetBoardSpace(index);
        
        var value = PropertySetHelper.GetSellValue(set, engine.Cache.Board);
        value = MoneyHelper.NormaliseAmountToPositive(value, engine.Cache.RoundingRule, FinancialReason.Build);
        
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = $"Sell on {space.Name}?",
            Body = $"Are you sure you want to sell all properties in the {set.ToDisplayName()} set and receive {RuleDictionary.Currency}{value}?",
            Cost = value,
            BoardIndex = index,
            Type = AcquirePropertyType.SellAllInSet
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        var indexes = PropertySetHelper.GetIndexes(set);
        await SellOnProperties(engine, indexes, ct);
    }

    public async Task SellOnAllProperties(Framework.GameEngine engine, CancellationToken ct)
    {
        //Sell on all properties in every set
        
        //Check the player can build on all properties in every set
        var canSell = engine.Cache.Game.CanDecreaseRentLevelForAll();
        if(!canSell)
            return;
        
        //Get the current player
        var player = engine.Cache.Game.CurrentPlayer();
        if (player is null)
            return;
        
        //Get list of owned properties (excluding mortgaged and reserved)
        var props = engine.Cache.Game.GetOwnedProperties(includeMortgaged: false, includeReserved: false);
        if (props.Count == 0)
            return;
        
        //Translate the owned properties into a list of sets where they own the complete set:
        var ownedSetsProperties = PropertySetHelper.GetOwnedSets(player.PlayerId, props);
        
        //Get the indexes of every property in the owned sets
        uint value = 0;
        var indexes = new List<ushort>();
        foreach (var set in ownedSetsProperties)
        {
            var iList = PropertySetHelper.GetIndexes(set);
            indexes.AddRange(iList);
            
            value += PropertySetHelper.GetSellValue(set, engine.Cache.Board);
        }
        
        value = MoneyHelper.NormaliseAmountToPositive(value, engine.Cache.RoundingRule, FinancialReason.Build);
        var response = await engine.PromptProvider.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = "Sell on all properties in every set?",
            Body = $"Are you sure you want to sell on all properties in every set and receive {RuleDictionary.Currency}{value}?",
            Cost = value,
            BoardIndex = 0,
            Type = AcquirePropertyType.SellAll
        }, ct: ct);
        
        if(!response.Accept)
            return;
        
        //Build on all properties in index list
        await SellOnProperties(engine, indexes, ct);
    }
    
    
    private async Task SellOnProperties(Framework.GameEngine engine, List<ushort> boardIndexes, CancellationToken ct,
        uint? doubleHotelValue = null)
    {
        if(boardIndexes.Any(i => !i.IsProperty()))
            //Any non-buildable properties and return false (cannot build)
            return;

        var player = engine.Cache.Game.CurrentPlayer();
        if (player is null)
            return;
        
        List<PropertyModel> properties = boardIndexes.Select(i => engine.Cache.Game.GetPropertySpace(i)).ToList()!;
        if(properties.Any(p => p == null!))
            return;
        
        foreach (var property in properties)
        {
            if(property.State != PropertyState.Owned)
                //Should never throw since validation checks have been done before calling this
                throw new InvalidOperationException("Property is not owned");

            switch (property.RentLevel)
            {
                //Should never throw since validation checks have been done before calling this
                case > RentLevel.DOUBLE_HOTEL:
                    throw new InvalidOperationException("Cannot build on property with rent level higher than double hotel");
                //Should never throw since validation checks have been done before calling this
                case <= RentLevel.SET:
                    throw new InvalidOperationException("Cannot build on property with rent level less than set");
            }
            
            property.RentLevel -= 1;
            
            var value = PropertySetHelper.GetSellValue(property.BoardIndex, engine.Cache.Board);
            await _transactionService.ReceiveForSell(engine, player, doubleHotelValue ?? value, property.BoardIndex, ct);
        }
    }

    #endregion
}