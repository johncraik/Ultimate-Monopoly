using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="MoneyAction"/> against <see cref="TransactionService"/> — a money
/// movement to/from the Bank or Free Parking, after realising the amount (per-unit scaling and
/// the percentage-card %cap). The <c>EachPlayer</c> loop and the highest/lowest-roller dice-off
/// are deferred (they need <c>DiceService</c>). See cards-design.md §3.
/// </summary>
public class MoneyActionService : ICardActionService<MoneyAction>
{
    private readonly TransactionService _transactionService;

    /// <summary>Creates the money-action handler over the transaction seam it routes through.</summary>
    public MoneyActionService(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>
    /// Realises the action's amount and applies it as a card payout (receive) or charge (pay)
    /// to/from the Bank or Free Parking. No-ops on a zero realised amount (e.g. a per-house charge
    /// with no buildings) and on the not-yet-wired player-to-player counterparties.
    /// </summary>
    /// <param name="engine">The game engine bundle the money movement mutates.</param>
    /// <param name="player">The card holder paying or receiving.</param>
    /// <param name="action">The money action to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResolveActionAsync(Services.Framework.GameEngine engine, PlayerModel player, MoneyAction action, CancellationToken ct)
    {
        // TODO: Deferred (per decisions): the EachPlayer loop (payer-POV per transactions.md §4) and the
        // Highest/LowestRoller dice-off (DiceService) aren't wired yet.
        if (action.Counterparty is MoneyCounterparty.EachPlayer
            or MoneyCounterparty.HighestRoller or MoneyCounterparty.LowestRoller)
            return;

        var amount = RealiseAmount(engine, player, action);
        if (amount == 0)
            return;

        var counterparty = action.Counterparty == MoneyCounterparty.FreeParking
            ? TransactionCounterparty.FreeParking
            : TransactionCounterparty.Bank;

        if (action.Direction == MoneyDirection.Receive)
            await _transactionService.ReceiveCardPayout(engine, player, amount, counterparty, null, ct);
        else
            await _transactionService.PayCardCharge(engine, player, amount, counterparty, null, ct);
    }
    
    /// <summary>
    /// Base amount scaled by the per-unit count (houses/hotels/properties owned) and, for percentage
    /// cards, the player's %cap (100/50/10). The dice multiplier is a deferred DiceService roll.
    /// </summary>
    private static uint RealiseAmount(Services.Framework.GameEngine engine, PlayerModel player, MoneyAction action)
    {
        long amount = action.Amount;

        var (houses, hotels) = engine.Cache.Game.GetHousesAndHotelsTaken(player.PlayerId);
        amount *= action.PerUnit switch
        {
            MoneyPerUnit.PerHouse => houses,
            MoneyPerUnit.PerHotel => hotels,
            MoneyPerUnit.PerProperty => engine.Cache.Game.GetOwnedProperties(player.PlayerId).Count,
            _ => 1
        };

        // TODO: DiceMultiplier — a fresh roll via DiceService (deferred, decision #1).

        if (action.PercentageApplies)
            amount = (amount * engine.Cache.Game.PlayerPercentCap(player.PlayerId)) / 100;

        // A genuinely-zero realised amount — a per-house/per-hotel charge with no buildings,
        // or a percentage that floors to nothing — is "nothing to pay". Short-circuit BEFORE
        // NormaliseAmount, whose never-round-to-zero rule would otherwise bump it to the grid
        // minimum. For a 0 input that bump is the NEGATIVE grid minimum (positive = amount > 0
        // is false at 0), which the (uint) cast then wraps into a huge charge. Returning 0 here
        // also lets the caller's `amount == 0` guard short-circuit correctly.
        if (amount <= 0)
            return 0;

        return (uint)MoneyHelper.NormaliseAmount(amount, engine.Cache.RoundingRule,
            action.Direction == MoneyDirection.Receive
                ? FinancialReason.CardPayout
                : FinancialReason.CardCharge);
    }
}