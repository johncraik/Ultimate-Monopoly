using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Players;

namespace MP.GameEngine.Models.Cards.Conditions;

/// <summary>
/// A condition that makes a held card live (cards-design.md §5): the <see cref="Trigger"/> event(s)
/// it waits on, plus any parameters that further gate it. A card's conditions are ORed (live if any
/// matches), as are the flags within a trigger. The play-mode (forced/choice, whose turn) is the
/// card's <see cref="CardConditionType"/>.
/// </summary>
public class CardCondition
{
    /// <summary>Stable identity (GUID), shared with the persisted card definition on re-import.</summary>
    public string ConditionId { get; set; }

    /// <summary>The event(s) that make the held card live — a <see cref="CardTrigger"/> flag set, ORed.</summary>
    public CardTrigger Trigger { get; set; }

    /// <summary>
    /// Optional travel-direction gate: when set, the condition is live only while the trigger's subject
    /// is moving in this direction (e.g. "receive £X when passing GO <i>anti-clockwise</i>" =
    /// <see cref="PlayerDirection.Backward"/> on an <see cref="CardTrigger.OnPassGo"/> condition). Null = any direction.
    /// </summary>
    public PlayerDirection? RequiredDirection { get; set; }

    /// <summary>
    /// Optional jail-state gate: <see cref="JailFilter.OnlyJailed"/> makes the condition live only while the
    /// subject is in jail ("a double in jail becomes a triple"), <see cref="JailFilter.OnlyNotJailed"/> only
    /// while out; <see cref="JailFilter.None"/> (default) = no gate.
    /// </summary>
    public JailFilter JailFilter { get; set; }

    public CardCondition()
    {
    }

    public CardCondition(CardCondition condition)
    {
        ConditionId = condition.ConditionId;
        Trigger = condition.Trigger;
        RequiredDirection = condition.RequiredDirection;
        JailFilter = condition.JailFilter;
    }
}