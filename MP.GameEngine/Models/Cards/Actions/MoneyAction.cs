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
    /// <summary>Base amount before per-unit / dice / percentage scaling.</summary>
    public long Amount { get; set; }

    public MoneyDirection Direction { get; set; }
    public MoneyCounterparty Counterparty { get; set; }

    /// <summary>Multiplies <see cref="Amount"/> by the player's houses / hotels / properties.</summary>
    public MoneyPerUnit PerUnit { get; set; }

    /// <summary>Multiplies the amount by a dice roll (e.g. "£200 × 1 die").</summary>
    public DiceMultiplier DiceMultiplier { get; set; }

    /// <summary>Percentage cards: the realised amount scales by the player's %cap (100 / 50 / 10).</summary>
    public bool PercentageApplies { get; set; }

    /// <summary>
    /// For a <see cref="MoneyCounterparty.HighestRoller"/> / <see cref="MoneyCounterparty.LowestRoller"/>
    /// dice-off, whether the card holder also rolls (and can win). When the holder wins their own
    /// dice-off the movement is with themselves — i.e. a no-op. Default false: only the other players
    /// roll. Ignored for every other counterparty.
    /// </summary>
    public bool IncludeHolderInRoll { get; set; }
}