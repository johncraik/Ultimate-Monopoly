using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Cards;

namespace MP.GameEngine.Services.SubSystems;

public class PurgingService
{
    private readonly CardImmunityService _immunityService;

    public PurgingService(CardImmunityService immunityService)
    {
        _immunityService = immunityService;
    }


    public async Task PurgeOwnProperty(Framework.GameEngine engine, PlayerModel player, ushort propCount, CancellationToken ct)
        => await PurgeProperty(engine, player, player, propCount, ct);

    public async Task PurgeOthersProperty(Framework.GameEngine engine, PlayerModel player, ushort propCount, CancellationToken ct)
    {
        var players = engine.Cache.Game.GetPlayers(player.PlayerId);
        var response = await engine.PromptProvider.RequestAsync(new TargetPlayerPrompt
        {
            PlayerId = player.PlayerId,
            Title = "Select a Player to Purge Properties",
            Body = "Which player would you like to purge properties from?",
            EligiblePlayerIds = players.Select(p => p.PlayerId).ToList(),
            Count = 1
        }, ct: ct);

        if(response.SelectedPlayerIds.Count == 0)
            throw new InvalidOperationException("No player selected");

        var purgingPlayer = engine.Cache.Game.GetPlayer(response.SelectedPlayerIds[0]);
        if(purgingPlayer is null)
            throw new InvalidOperationException("Player not found");

        var result = await _immunityService.CheckPurgingPropertyImmunity(engine, purgingPlayer, ct);
        if (result)
        {
            engine.Notifier.Notify(engine.Cache.GameId, player.PlayerId, 
                "Player played an immunity card. Their properties will not be purged");
            engine.Notifier.Notify(engine.Cache.GameId, purgingPlayer.PlayerId, 
                "You played an immunity card. Your properties will not be purged");
            return;
        }
        
        //The HOLDER chooses which of the opponent's properties to purge: chooser = player, owner = opponent.
        await PurgeProperty(engine, player, purgingPlayer, propCount, ct);
    }

    private async Task PurgeProperty(Framework.GameEngine engine, PlayerModel chooser, PlayerModel owner, ushort propCount, CancellationToken ct)
    {
        //The OWNER's built-on properties are the eligible set; the CHOOSER (the holder, or the owner for a
        //self-purge) is the one prompted to pick — never the opponent whose property is being purged.
        var eligibleProps = engine.Cache.Game.BuiltOnProperties(owner.PlayerId);
        eligibleProps = eligibleProps.Where(p => !p.HasBeenPurged).ToList();
        if (eligibleProps.Count == 0)
        {
            _ = await engine.PromptProvider.Acknowledge(chooser.PlayerId, "No Properties to Purge",
                chooser.PlayerId == owner.PlayerId
                    ? "You have no eligible properties to purge. Lucky you!"
                    : "The selected player has no eligible properties to purge.", ct: ct);
            return;
        }

        var response = await engine.PromptProvider.RequestAsync(new TargetPropertyPrompt
        {
            PlayerId = chooser.PlayerId,
            Title = propCount == 1 ? "Purge a Property" : "Purge Properties",
            Body = propCount == 1 ? "Which property would you like to purge?" : $"Which {propCount} properties would you like to purge?",
            EligibleBoardIndexes = eligibleProps.Select(p => p.BoardIndex).ToList(),
            Count = propCount
        }, ct: ct);

        if(response.SelectedBoardIndexes.Count == 0)
            throw new InvalidOperationException("No property selected");

        if(response.SelectedBoardIndexes.Any(i => !eligibleProps.Select(p => p.BoardIndex).Contains(i)))
            throw new InvalidOperationException("Invalid property selected");

        //Receipt attributes the purge to the OWNER (their property lost its buildings), not the chooser.
        PurgeProperties(engine, owner, response.SelectedBoardIndexes.ToList());

        _ = await engine.PromptProvider.Acknowledge(chooser.PlayerId,
             propCount == 1 ? "Property Purged" : "Properties Purged",
             propCount == 1 ? "Property has been purged" : "Properties have been purged", ct: ct);
    }

    
    public void PurgeProperties(Framework.GameEngine engine, PlayerModel player, List<ushort> boardIndexes)
    {
        foreach (var index in boardIndexes)
        {
            var property = engine.Cache.Game.GetPropertySpace(index);
            if(property is null)
                throw new InvalidOperationException("Property not found");
            
            property.IsPurged = true;
            property.HasBeenPurged = true;
            property.RentLevel = RentLevel.SET;
            
            engine.EventEmitter.Emit(new PropertyPurgedReceipt
            {
                PlayerId = player.PlayerId,
                PropertyBoardIndex = index
            });
        }
    }
}