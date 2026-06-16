using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="PropertyAction"/> — moving title(s) between a player, the bank, and
/// the Free Parking pot, routed to <see cref="PropertyTransferService"/>. Covers returning /
/// handing-in a single property or a whole <see cref="PropertyAction.Set"/>, taking an available
/// property from the bank, receiving all Free-Parking-held properties, and clearing the Free Parking
/// pot to the bank. A player with no eligible property is a silent no-op. See cards-design.md §3.
/// </summary>
public class PropertyActionService : ICardActionService<PropertyAction>
{
    private readonly PropertyTransferService _propertyTransferService;
    private readonly PurgingService _purgingService;
    private readonly PropertyService _propertyService;

    /// <summary>Creates the property-action handler over the title-transfer, purge and normalisation seams it routes through.</summary>
    public PropertyActionService(PropertyTransferService propertyTransferService,
        PurgingService purgingService,
        PropertyService propertyService)
    {
        _propertyTransferService = propertyTransferService;
        _purgingService = purgingService;
        _propertyService = propertyService;
    }

    /// <summary>Dispatches by kind.</summary>
    public async Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, PropertyAction action, CancellationToken ct, CardActionContext? context = null)
    {
        switch (action.Kind)
        {
            case PropertyActionKind.ReturnToBank:
            case PropertyActionKind.HandInToFreeParking:
                foreach (var owner in await CardActionHelper.ResolveTargets(engine, player, action.Target, ct))
                    await Relinquish(engine, owner, action, ct);
                break;

            case PropertyActionKind.TakeFromBank:
                await TakeFromBank(engine, player, action.Count, ct);
                break;

            case PropertyActionKind.ReceiveAllFreeParking:
                ReceiveAllFreeParking(engine, player);
                break;

            case PropertyActionKind.ClearFreeParkingToBank:
                ClearFreeParkingToBank(engine);
                break;

            case PropertyActionKind.SwapSet:
                await SwapSet(engine, player, ct);
                break;
        }

        return true;
    }

    /// <summary>
    /// Each targeted owner gives up either a whole chosen set (<see cref="PropertyAction.Set"/>) or
    /// <see cref="PropertyAction.Count"/> chosen properties, to the bank or Free Parking.
    /// </summary>
    private async Task Relinquish(Framework.GameEngine engine, PlayerModel owner, PropertyAction action, CancellationToken ct)
    {
        var toFp = action.Kind == PropertyActionKind.HandInToFreeParking;

        var indexes = action.Set
            ? await ChooseSetIndexes(engine, owner, ct)
            : await ChooseProperties(engine, owner, action.Count, toFp, ct);

        foreach (var index in indexes)
        {
            var property = engine.Cache.Game.GetPropertySpace(index);
            if (property is null)
                continue;

            if (toFp)
                _propertyTransferService.HandIntoFreeParking(engine, owner, property);
            else
                _propertyTransferService.ReturnToBank(engine, owner, property);
        }
    }

    /// <summary>Prompts the owner for up to <paramref name="count"/> of their tradable properties.</summary>
    private static async Task<List<ushort>> ChooseProperties(Framework.GameEngine engine, PlayerModel owner, ushort count, bool toFp, CancellationToken ct)
    {
        var eligible = engine.Cache.Game.TradableProperties(owner.PlayerId);
        if (eligible.Count == 0)
            return [];

        var pick = (ushort)Math.Min((int)count, eligible.Count);
        var response = await engine.PromptProvider.RequestAsync(new TargetPropertyPrompt
        {
            PlayerId = owner.PlayerId,
            Title = toFp ? "Hand In a Property" : "Return a Property",
            Body = toFp ? "Choose a property to hand into Free Parking." : "Choose a property to return to the bank.",
            EligibleBoardIndexes = eligible.Select(p => p.BoardIndex).ToList(),
            Count = pick
        }, ct);

        var eligibleIndexes = eligible.Select(p => p.BoardIndex).ToHashSet();
        return response.SelectedBoardIndexes.Where(eligibleIndexes.Contains).ToList();
    }

    /// <summary>
    /// Resolves which complete set the owner gives up — no prompt when they hold exactly one (or
    /// none → empty), otherwise the owner picks via a representative property of each owned set.
    /// Returns all board indexes in the chosen set.
    /// </summary>
    private static async Task<List<ushort>> ChooseSetIndexes(Framework.GameEngine engine, PlayerModel owner, CancellationToken ct)
    {
        var owned = engine.Cache.Game.GetOwnedProperties(owner.PlayerId, includeReserved: false);
        var sets = PropertySetHelper.GetOwnedSets(owner.PlayerId, owned);
        if (sets.Count == 0)
            return [];

        PropertySet chosen;
        if (sets.Count == 1)
        {
            chosen = sets[0];
        }
        else
        {
            // One representative property per owned set; the chosen index maps back to its set.
            var representatives = sets
                .Select(s => engine.Cache.Game.GetOwnedProperties(owner.PlayerId, s).First().BoardIndex)
                .ToList();

            var response = await engine.PromptProvider.RequestAsync(new TargetPropertyPrompt
            {
                PlayerId = owner.PlayerId,
                Title = "Return a Set",
                Body = "Choose a property in the set you want to give up.",
                EligibleBoardIndexes = representatives,
                Count = 1
            }, ct);

            if (response.SelectedBoardIndexes.Count == 0)
                return [];

            var resolved = PropertySetHelper.ResolveSet(response.SelectedBoardIndexes[0]);
            if (resolved is null)
                return [];
            chosen = (PropertySet)resolved;
        }

        return engine.Cache.Game.GetOwnedProperties(owner.PlayerId, chosen).Select(p => p.BoardIndex).ToList();
    }

    /// <summary>Lets the holder take up to <paramref name="count"/> available (bank-owned) properties, free of charge.</summary>
    private async Task TakeFromBank(Framework.GameEngine engine, PlayerModel player, ushort count, CancellationToken ct)
    {
        var available = engine.Cache.Game.Properties
            .Where(p => p.OwnerPlayerId is null && p.State == PropertyState.NotOwned)
            .ToList();
        if (available.Count == 0)
            return;

        var pick = (ushort)Math.Min((int)count, available.Count);
        var response = await engine.PromptProvider.RequestAsync(new TargetPropertyPrompt
        {
            PlayerId = player.PlayerId,
            Title = "Choose a Property",
            Body = "Choose any available property from the bank.",
            EligibleBoardIndexes = available.Select(p => p.BoardIndex).ToList(),
            Count = pick
        }, ct);

        var availableIndexes = available.Select(p => p.BoardIndex).ToHashSet();
        foreach (var index in response.SelectedBoardIndexes.Where(availableIndexes.Contains))
        {
            var property = engine.Cache.Game.GetPropertySpace(index);
            if (property is not null)
                _propertyTransferService.Buy(engine, player, property);   // free acquisition — no money leg
        }
    }

    /// <summary>Moves every Free-Parking-held property to the holder.</summary>
    private void ReceiveAllFreeParking(Framework.GameEngine engine, PlayerModel player)
    {
        foreach (var property in engine.Cache.Game.Properties.Where(p => p.State == PropertyState.FreeParking).ToList())
            _propertyTransferService.TakeFromFreeParking(engine, player, property);
    }

    /// <summary>Returns every Free-Parking property — and the FP money pot — to the bank.</summary>
    private void ClearFreeParkingToBank(Framework.GameEngine engine)
    {
        foreach (var property in engine.Cache.Game.Properties.Where(p => p.State == PropertyState.FreeParking).ToList())
            property.ReturnToBank();

        engine.Cache.Game.FreeParkingAmount = 0;
    }

    /// <summary>
    /// Swaps one of the holder's complete buildable sets for one of a chosen player's complete sets —
    /// every title in each set changes hands — then purges both swapped sets (now under their new
    /// owners). Silent no-op when the holder holds no complete set, or no other player does.
    /// </summary>
    private async Task SwapSet(Framework.GameEngine engine, PlayerModel holder, CancellationToken ct)
    {
        // A player who holds a complete buildable set — the holder picks who to swap with.
        var candidates = engine.Cache.Game.GetPlayers(holder.PlayerId)
            .Where(p => CompleteSets(engine, p.PlayerId).Count > 0)
            .ToList();
        if (candidates.Count == 0)
            return;
        
        // The holder's set to give up.
        var holderSet = await ChooseCompleteSet(engine, holder.PlayerId, holder.PlayerId,
            "Swap a Set", "Choose one of your sets to swap away.", ct);
        if (holderSet is null)
            return;
        
        //Prompt to choose a player
        var targetResponse = await engine.PromptProvider.RequestAsync(new TargetPlayerPrompt
        {
            PlayerId = holder.PlayerId,
            Title = "Swap a Set",
            Body = "Choose a player to swap a set with.",
            EligiblePlayerIds = candidates.Select(p => p.PlayerId).ToList(),
            Count = 1
        }, ct);

        var target = engine.Cache.Game.GetPlayer(targetResponse.SelectedPlayerIds.FirstOrDefault() ?? string.Empty);
        if (target is null)
            return;

        // The target's set the holder takes.
        var targetSet = await ChooseCompleteSet(engine, holder.PlayerId, target.PlayerId,
            "Choose a Set", "Choose a set to take from this player.", ct);
        if (targetSet is null)
            return;

        // Snapshot both sets' indexes before any title moves (the two sets are distinct colours, so
        // they never overlap — snapshot regardless to keep the two transfers independent).
        var holderIndexes = PropertySetHelper.GetIndexes(holderSet.Value);
        var targetIndexes = PropertySetHelper.GetIndexes(targetSet.Value);

        TransferSet(engine, holder, target, holderIndexes);   // holder's set → target
        TransferSet(engine, target, holder, targetIndexes);   // target's set → holder

        // Both swapped sets are purged, attributed to their new owners.
        _purgingService.PurgeProperties(engine, target, holderIndexes);
        _purgingService.PurgeProperties(engine, holder, targetIndexes);

        engine.Cache.Game.CheckReservationRuleSetObtained(holder.PlayerId);
        engine.Cache.Game.CheckReservationRuleSetObtained(target.PlayerId);
        _propertyService.NormaliseProperties(engine);
    }

    /// <summary>The complete buildable sets <paramref name="playerId"/> owns (reserved properties excluded).</summary>
    private static List<PropertySet> CompleteSets(Framework.GameEngine engine, string playerId)
        => PropertySetHelper.GetOwnedSets(playerId,
            engine.Cache.Game.GetOwnedProperties(playerId, includeReserved: false));

    /// <summary>
    /// Prompts <paramref name="chooserId"/> to pick one of <paramref name="ownerId"/>'s complete buildable
    /// sets (no prompt when they hold exactly one; null when they hold none), via a representative property
    /// of each set. Returns the chosen set.
    /// </summary>
    private async Task<PropertySet?> ChooseCompleteSet(Framework.GameEngine engine, string chooserId, string ownerId,
        string title, string body, CancellationToken ct)
    {
        var sets = CompleteSets(engine, ownerId);
        if (sets.Count == 0)
            return null;
        if (sets.Count == 1)
            return sets[0];

        var representatives = sets
            .Select(s => engine.Cache.Game.GetOwnedProperties(ownerId, s).First().BoardIndex)
            .ToList();

        var response = await engine.PromptProvider.RequestAsync(new TargetPropertyPrompt
        {
            PlayerId = chooserId,
            Title = title,
            Body = body,
            EligibleBoardIndexes = representatives,
            Count = 1
        }, ct);

        return response.SelectedBoardIndexes.Count == 0
            ? null
            : PropertySetHelper.ResolveSet(response.SelectedBoardIndexes[0]);
    }

    /// <summary>Moves every title at <paramref name="indexes"/> from one player to another.</summary>
    private void TransferSet(Framework.GameEngine engine, PlayerModel from, PlayerModel to, List<ushort> indexes)
    {
        foreach (var index in indexes)
        {
            var property = engine.Cache.Game.GetPropertySpace(index);
            if (property is not null)
                _propertyTransferService.Transfer(engine, from, to, property);
        }
    }
}
