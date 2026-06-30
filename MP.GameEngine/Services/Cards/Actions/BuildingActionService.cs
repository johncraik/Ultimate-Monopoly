using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="BuildingAction"/> — purging built-on properties (via
/// <see cref="PurgingService"/>) or granting a free hotel (bumping a chosen four-house property to a
/// hotel when one is available, then re-normalising via <see cref="PropertyService"/>). See
/// cards-design.md §3 (Building).
/// </summary>
public class BuildingActionService : ICardActionService<BuildingAction>
{
    private readonly PurgingService _purgingService;
    private readonly PropertyService _propertyService;

    /// <summary>Creates the building-action handler over the purge and property-normalisation seams.</summary>
    public BuildingActionService(PurgingService purgingService, PropertyService propertyService)
    {
        _purgingService = purgingService;
        _propertyService = propertyService;
    }

    /// <summary>Applies the building action.</summary>
    public async Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, BuildingAction action, CancellationToken ct, CardActionContext? context = null)
    {
        switch (action.Kind)
        {
            case BuildingKind.Purge:
                if (action.Target == PlayerTarget.ChosenPlayer)
                    await _purgingService.PurgeOthersProperty(engine, player, action.Count, ct);
                else
                    await _purgingService.PurgeOwnProperty(engine, player, action.Count, ct);
                break;

            case BuildingKind.GrantHotel:
                await GrantHotel(engine, player, ct);
                break;
        }
        
        return true;
    }

    /// <summary>
    /// Grants a free hotel (R-06). Two outcomes:
    /// <list type="number">
    /// <item>A hotel is available in the pool <b>and</b> the holder has a four-house street ready — the
    /// holder picks which four-house property becomes a hotel and it is placed now, free of charge.</item>
    /// <item>Otherwise (no four-house street, or the pool is empty) the holder is granted a held
    /// <see cref="PlayerModel.FreeHotels"/> credit instead, which waives the build cost of a future hotel
    /// (consumed in <c>BuildingService</c>). So the card always gives something — never a silent no-op.</item>
    /// </list>
    /// </summary>
    private async Task GrantHotel(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var (_, hotelsLeft) = engine.Cache.Game.GetHousesAndHotelsLeft();
        var eligible = engine.Cache.Game.GetOwnedProperties(player.PlayerId)
            .Where(p => p.RentLevel == RentLevel.FOUR_HOUSES)
            .ToList();

        //Case 1: a hotel is in the pool AND the holder has a four-house street — place it now.
        if (hotelsLeft > 0 && eligible.Count > 0)
        {
            var response = await engine.PromptProvider.RequestAsync(new TargetPropertyPrompt
            {
                PlayerId = player.PlayerId,
                Title = "Free Hotel",
                Body = "Choose a property to receive a free hotel.",
                EligibleBoardIndexes = eligible.Select(p => p.BoardIndex).ToList(),
                Count = 1
            }, ct);

            if (response.SelectedBoardIndexes.Count == 0)
                return;

            var property = engine.Cache.Game.GetPropertySpace(response.SelectedBoardIndexes[0]);
            if (property is null || property.RentLevel != RentLevel.FOUR_HOUSES)
                return;

            property.RentLevel = RentLevel.HOTEL;
            _propertyService.NormaliseProperties(engine);
            return;
        }

        //Case 2 / 2.5: can't place a hotel right now (no four-house street, or the pool is empty) — grant a
        //held free-hotel credit. It makes a future hotel build free (BuildingService.BuildOnProperties).
        player.FreeHotels++;
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Free Hotel",
            "You receive a free hotel. Your next hotel build will be free.", ct: ct);
    }
}
