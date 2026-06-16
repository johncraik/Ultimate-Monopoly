using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.Cards.Actions;

/// <summary>
/// A money movement driven by a card, resolved by the card interpreter against
/// <c>TransactionService</c>. Covers the bulk of the Money inventory; the
/// highest/lowest-roller counterparties require a one-die dice-off to resolve the
/// target player. See <c>design-docs/cards-design.md</c> §3 / <c>cards-actions.md</c>.
/// </summary>
public sealed class MoneyAction : CardAction
{
    /// <summary>
    /// Base amount before per-unit / dice / percentage scaling. Read as a <i>percent</i> for the percentage
    /// <see cref="Basis"/> values, and as the <i>factor</i> for <see cref="AmountSource.TriggerAmount"/> —
    /// decimal so fractional factors are expressible (e.g. ×0.5 "pay half the tax" = <c>Amount = 0.5</c>).
    /// </summary>
    public decimal Amount { get; set; }

    public MoneyDirection Direction { get; set; }
    public MoneyCounterparty Counterparty { get; set; }

    /// <summary>
    /// Whose money moves. <see cref="PlayerTarget.Self"/> (default) = the holder, with
    /// <see cref="Counterparty"/> driving where it flows (the original behaviour). Any other value =
    /// each targeted player is the subject of a Bank / Free Parking move ("each player receives
    /// £1000 from the bank").
    /// </summary>
    public PlayerTarget Target { get; set; } = PlayerTarget.Self;

    /// <summary>Where the amount is derived from (a fixed figure, a fraction of cash / the FP pot, the triple bonus, …).</summary>
    public MoneyAmountBasis Basis { get; set; } = MoneyAmountBasis.Fixed;

    /// <summary>
    /// Whether the base figure comes from the action (<see cref="AmountSource.Fixed"/>, the default —
    /// uses <see cref="Amount"/> / <see cref="Basis"/>) or from the firing trigger
    /// (<see cref="AmountSource.TriggerAmount"/> — the <c>CardActionContext.TriggerAmount</c>, with
    /// <see cref="Amount"/> reused as the factor). Overrides <see cref="Basis"/> when set to
    /// <see cref="AmountSource.TriggerAmount"/>. See card-triggers.md §6.
    /// </summary>
    public AmountSource AmountSource { get; set; } = AmountSource.Fixed;

    /// <summary>
    /// When true, swaps the holder's entire cash balance with the counterparty — a chosen player,
    /// or the highest/lowest dice-off roller (per <see cref="Counterparty"/>). Ignores the amount.
    /// </summary>
    public bool SwapCash { get; set; }

    /// <summary>Multiplies <see cref="Amount"/> by the player's houses / hotels / properties.</summary>
    public MoneyPerUnit PerUnit { get; set; }

    /// <summary>Multiplies the amount by a dice roll (e.g. "£200 × 1 die").</summary>
    public DiceMultiplier DiceMultiplier { get; set; }

    /// <summary>Percentage cards: the realised amount scales by the player's %cap (100 / 50 / 10).</summary>
    public bool PercentageApplies { get; set; }

    /// <summary>
    /// The dice-off that picks the player when <see cref="Counterparty"/> is
    /// <see cref="MoneyCounterparty.DiceOffPlayer"/>, or the swap partner when <see cref="SwapCash"/> is
    /// set without a chosen player. Carries the dice count, highest/lowest, and whether the holder
    /// joins the roll. When the holder wins their own dice-off the movement is with themselves — a no-op.
    /// </summary>
    public DiceOff? DiceOff { get; set; }
}