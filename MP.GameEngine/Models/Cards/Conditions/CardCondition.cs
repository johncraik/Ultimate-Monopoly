using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.Cards.Conditions;

/// <summary>
/// A condition that makes a held card live (cards-design.md §5): the <see cref="Trigger"/> event(s)
/// it waits on. A card's conditions are ORed (live if any matches), as are the flags within a
/// trigger. The play-mode (forced/choice, whose turn) is the card's <see cref="CardConditionType"/>.
/// </summary>
public class CardCondition
{
    /// <summary>Stable identity (GUID), shared with the persisted card definition on re-import.</summary>
    public string ConditionId { get; set; }

    /// <summary>The event(s) that make the held card live — a <see cref="CardTrigger"/> flag set, ORed.</summary>
    public CardTrigger Trigger { get; set; }
}