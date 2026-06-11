using JC.Core.Extensions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class BuildingService
{
    private readonly PropertyService _propertyService;
    private readonly TransactionService _transactionService;

    public BuildingService(PropertyService propertyService,
        TransactionService transactionService)
    {
        _propertyService = propertyService;
        _transactionService = transactionService;
    }
    
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
        if(property is null || space.PropertySet is null)
            return;
        
        var streetEffect = engine.Cache.Game.HasStreetEffect((PropertySet)space.PropertySet);
        var cost = PropertySetHelper.GetBuildCost(boardIndex, engine.Cache.Board, streetEffect);
        uint? doubleHotelCost = null;
        if (property.RentLevel == RentLevel.HOTEL)
        {
            //Property has a hotel, and we have passed increase rent level check
            //Therefore, we must be building a double hotel (build cost * 5)
            doubleHotelCost = PropertySetHelper.GetDoubleHotelCost(boardIndex, engine.Cache.Board, streetEffect);
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
        if(space.PropertySet is null)
            return;
        
        var streetEffect = engine.Cache.Game.HasStreetEffect((PropertySet)space.PropertySet);
        var cost = PropertySetHelper.GetBuildCost(set, engine.Cache.Board, streetEffect);
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
            Title = $"Build on all properties in the {set.ToDisplayName()} set?",
            Body = $"Are you sure you want to spend {RuleDictionary.Currency}{cost} to build on all properties in the {set.ToDisplayName()} set?" +
                   $"{(streetEffect ? " Note: you have qualified for the street rule, building cost is half price!" : "")}",
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
            
            var streetEffect = engine.Cache.Game.HasStreetEffect(set);
            cost += PropertySetHelper.GetBuildCost(set, engine.Cache.Board, streetEffect);
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
            property.StreetRuleQualifier = property.StreetRuleQualifier == StreetRuleQualifier.Qualified 
                ? StreetRuleQualifier.Qualified
                : StreetRuleQualifier.BuiltOn;
            
            var space = engine.Cache.Board.GetBoardSpace(property.BoardIndex);
            if(space.PropertySet is null)
                throw new InvalidOperationException("Property space has no property set");
            
            var streetEffect = engine.Cache.Game.HasStreetEffect((PropertySet)space.PropertySet);
            var cost = PropertySetHelper.GetBuildCost(property.BoardIndex, engine.Cache.Board, streetEffect);
            await _transactionService.PayForBuild(engine, player, doubleHotelCost ?? cost, property.BoardIndex, ct);
        }
        
        _propertyService.NormaliseProperties(engine);
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
        if(property is null || space.PropertySet is null)
            return;
        
        var streetEffect = engine.Cache.Game.HasStreetEffect((PropertySet)space.PropertySet);
        var value = PropertySetHelper.GetSellValue(boardIndex, engine.Cache.Board, streetEffect);
        uint? doubleHotelValue = null;
        if (property.RentLevel == RentLevel.HOTEL)
        {
            //Property has a double hotel, and we have passed decreased rent level check
            //Therefore, we must be selling a double hotel (build cost * 5)/2
            doubleHotelValue = PropertySetHelper.GetDoubleHotelSellValue(boardIndex, engine.Cache.Board, streetEffect);
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
        if(space.PropertySet is null)
            return;
        
        var streetEffect = engine.Cache.Game.HasStreetEffect((PropertySet)space.PropertySet);
        var value = PropertySetHelper.GetSellValue(set, engine.Cache.Board, streetEffect);
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
            
            var streetEffect = engine.Cache.Game.HasStreetEffect(set);
            value += PropertySetHelper.GetSellValue(set, engine.Cache.Board, streetEffect);
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
            
            var space = engine.Cache.Board.GetBoardSpace(property.BoardIndex);
            if(space.PropertySet is null)
                throw new InvalidOperationException("Property space has no property set");
            
            var streetEffect = engine.Cache.Game.HasStreetEffect((PropertySet)space.PropertySet);
            var value = PropertySetHelper.GetSellValue(property.BoardIndex, engine.Cache.Board, streetEffect);
            await _transactionService.ReceiveForSell(engine, player, doubleHotelValue ?? value, property.BoardIndex, ct);
        }
        
        _propertyService.NormaliseProperties(engine);
    }
    
    #endregion
}