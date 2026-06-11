using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class PropertyCommandService
{
    private readonly PropertyService _propertyService;
    private readonly TransactionService _transactionService;

    public PropertyCommandService(PropertyService propertyService, 
        TransactionService transactionService)
    {
        _propertyService = propertyService;
        _transactionService = transactionService;
    }
    
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
        
        var cost = _propertyService.GetPropertyCost(engine, player, space, property);
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
        _propertyService.NormaliseProperties(engine);
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
        _propertyService.NormaliseProperties(engine);
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
        _propertyService.NormaliseProperties(engine);
    }
}