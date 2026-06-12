using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="MoneyAction"/> — a money movement realised from the holder's
/// per-unit count, an optional <see cref="DiceMultiplier"/> (a fresh holder roll), and the
/// percentage-card %cap, then routed to its counterparty: the Bank / Free Parking, every other
/// active player (<see cref="MoneyCounterparty.EachPlayer"/>), or the winner of a one-die dice-off
/// (<see cref="MoneyCounterparty.HighestRoller"/> / <see cref="MoneyCounterparty.LowestRoller"/>).
/// Every player-to-player movement runs from the <b>payer's</b> POV, so each payer's affordability
/// (and any shortfall) is its own — never the receiver's (transactions.md §4). See cards-design.md §3.
/// </summary>
public class MoneyActionService : ICardActionService<MoneyAction>
{
    private readonly TransactionService _transactionService;
    private readonly DiceService _diceService;

    /// <summary>Creates the money-action handler over the transaction seam and the dice seam it routes through.</summary>
    public MoneyActionService(TransactionService transactionService, DiceService diceService)
    {
        _transactionService = transactionService;
        _diceService = diceService;
    }

    /// <summary>
    /// Rolls the dice multiplier (if any), realises the amount, and applies it to the action's
    /// counterparty. No-ops on a zero realised amount (e.g. a per-house charge with no buildings).
    /// </summary>
    /// <param name="engine">The game engine bundle the money movement mutates.</param>
    /// <param name="player">The card holder paying or receiving.</param>
    /// <param name="action">The money action to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, MoneyAction action, CancellationToken ct)
    {
        // A dice multiplier is a fresh roll by the holder, folded into the realised amount.
        var diceMultiplier = await RollDiceMultiplier(engine, player, action.DiceMultiplier, ct);

        var amount = RealiseAmount(engine, player, action, diceMultiplier);
        if (amount == 0)
            return;

        switch (action.Counterparty)
        {
            case MoneyCounterparty.EachPlayer:
                await ApplyToEachPlayer(engine, player, action.Direction, amount, ct);
                break;
            case MoneyCounterparty.HighestRoller:
            case MoneyCounterparty.LowestRoller:
                await ApplyToDiceOffWinner(engine, player, action.Direction,
                    highest: action.Counterparty == MoneyCounterparty.HighestRoller,
                    includeHolder: action.IncludeHolderInRoll, amount, ct);
                break;
            default:
                await ApplyToBankOrFreeParking(engine, player, action.Direction, action.Counterparty, amount, ct);
                break;
        }
    }


    /// <summary>Bank / Free Parking — the holder simply pays or receives the amount (no shortfall on a receive).</summary>
    private async Task ApplyToBankOrFreeParking(Framework.GameEngine engine, PlayerModel holder,
        MoneyDirection direction, MoneyCounterparty counterparty, uint amount, CancellationToken ct)
    {
        var target = counterparty == MoneyCounterparty.FreeParking
            ? TransactionCounterparty.FreeParking
            : TransactionCounterparty.Bank;

        if (direction == MoneyDirection.Receive)
            await _transactionService.ReceiveCardPayout(engine, holder, amount, target, null, ct);
        else
            await _transactionService.PayCardCharge(engine, holder, amount, target, null, ct);
    }


    /// <summary>
    /// Every other active player. Always driven from the payer's POV (transactions.md §4): when the
    /// holder PAYS, the holder is the payer in each move; when the holder RECEIVES (e.g. "collect £50
    /// from each player"), each other player is the payer — so a payer who can't afford it gets their
    /// own shortfall, and a bankrupt one doesn't break the others.
    /// </summary>
    private async Task ApplyToEachPlayer(Framework.GameEngine engine, PlayerModel holder,
        MoneyDirection direction, uint amount, CancellationToken ct)
    {
        foreach (var other in engine.Cache.Game.GetPlayers(holder.PlayerId))
        {
            if (direction == MoneyDirection.Pay)
                await _transactionService.PayCardCharge(engine, holder, amount, TransactionCounterparty.Player, other, ct);
            else
                await _transactionService.PayCardCharge(engine, other, amount, TransactionCounterparty.Player, holder, ct);
        }
    }


    /// <summary>
    /// The highest/lowest roller of a one-die dice-off is the single counterparty; the holder then
    /// pays them (or receives from them, payer-POV). The other active players always roll; the holder
    /// joins (and can win) when <paramref name="includeHolder"/> is set. No-op when there are no
    /// candidates, or when the holder wins their own dice-off (the movement would be with themselves).
    /// </summary>
    private async Task ApplyToDiceOffWinner(Framework.GameEngine engine, PlayerModel holder,
        MoneyDirection direction, bool highest, bool includeHolder, uint amount, CancellationToken ct)
    {
        var candidates = engine.Cache.Game.GetPlayers(holder.PlayerId, excludePovPlayer: !includeHolder);
        if (candidates.Count == 0)
            return;

        var winner = await RollDiceOff(engine, candidates, highest, ct);
        if (winner is null || winner.PlayerId == holder.PlayerId)
            return;

        if (direction == MoneyDirection.Pay)
            await _transactionService.PayCardCharge(engine, holder, amount, TransactionCounterparty.Player, winner, ct);
        else
            await _transactionService.PayCardCharge(engine, winner, amount, TransactionCounterparty.Player, holder, ct);
    }


    /// <summary>
    /// Each candidate rolls one die; the highest (or lowest) roller wins. Ties resolve to the first
    /// roller in clockwise order (strict comparison keeps the earlier winner).
    /// </summary>
    private async Task<PlayerModel?> RollDiceOff(Framework.GameEngine engine, List<PlayerModel> candidates,
        bool highest, CancellationToken ct)
    {
        PlayerModel? winner = null;
        var best = highest ? int.MinValue : int.MaxValue;

        foreach (var candidate in candidates)
        {
            var roll = await _diceService.RollCardDice(engine, candidate, 1,
                "Dice-off", "Roll one die for the card dice-off.", ct);
            int value = roll.Die1;

            if (highest ? value > best : value < best)
            {
                best = value;
                winner = candidate;
            }
        }

        return winner;
    }


    /// <summary>The holder's dice-multiplier roll summed (1 or 2 dice); 1 when the action has no multiplier.</summary>
    private async Task<uint> RollDiceMultiplier(Framework.GameEngine engine, PlayerModel holder,
        DiceMultiplier multiplier, CancellationToken ct)
    {
        if (multiplier == DiceMultiplier.None)
            return 1;

        var count = multiplier == DiceMultiplier.TwoDice ? (ushort)2 : (ushort)1;
        var roll = await _diceService.RollCardDice(engine, holder, count,
            "Roll the dice", "Roll the dice to determine the card amount.", ct);

        return count >= 2 ? (uint)(roll.Die1 + (roll.Die2 ?? 0)) : roll.Die1;
    }


    /// <summary>
    /// Base amount scaled by the per-unit count (houses/hotels/properties owned), the
    /// <paramref name="diceMultiplier"/> (a pre-rolled holder total), and — for percentage cards —
    /// the player's %cap (100/50/10), in that order. Returns 0 for a genuinely-zero realised amount.
    /// </summary>
    private static uint RealiseAmount(Framework.GameEngine engine, PlayerModel player, MoneyAction action, uint diceMultiplier)
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

        amount *= diceMultiplier;

        if (action.PercentageApplies)
            amount = (amount * engine.Cache.Game.PlayerPercentCap(player.PlayerId)) / 100;

        // A genuinely-zero realised amount — a per-house/per-hotel charge with no buildings, or a
        // percentage that floors to nothing — is "nothing to pay". Short-circuit BEFORE NormaliseAmount,
        // whose never-round-to-zero rule would otherwise bump it to the grid minimum. For a 0 input that
        // bump is the NEGATIVE grid minimum (positive = amount > 0 is false at 0), which the (uint) cast
        // then wraps into a huge charge. Returning 0 here also lets the caller's `amount == 0` guard fire.
        if (amount <= 0)
            return 0;

        return (uint)MoneyHelper.NormaliseAmount(amount, engine.Cache.RoundingRule,
            action.Direction == MoneyDirection.Receive
                ? FinancialReason.CardPayout
                : FinancialReason.CardCharge);
    }
}