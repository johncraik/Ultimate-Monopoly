using System.Text.Json.Serialization;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Conditions;

namespace MP.GameEngine.Models.Snapshot.Cards;

/// <summary>
/// A single card as pure snapshot data (cards-design.md §2/§10): its display text, the deck it
/// belongs to (<see cref="CardType"/>), the choosable <see cref="Groups"/> of actions, and the
/// <see cref="Conditions"/>/<see cref="ConditionType"/> that say when a held card may be played.
/// All behaviour lives in <see cref="Services.Cards.CardService"/> and the action services.
/// </summary>
public class CardModel
{
    /// <summary>Stable identity (GUID), shared with the persisted card definition on re-import.</summary>
    public string CardId { get; set; }
    /// <summary>Player-facing card text.</summary>
    public string CardText { get; set; }

    /// <summary>The deck this card belongs to and the rule by which it is drawn.</summary>
    public CardType CardType { get; set; }

    /// <summary>The choosable options on the card — ORed; each group's actions are ANDed (§2).</summary>
    public IReadOnlyList<CardGroup> Groups { get; set; } = [];
    /// <summary>When a held card becomes live — ORed triggers (§5). Empty for resolve-on-draw cards.</summary>
    public IReadOnlyList<CardCondition> Conditions { get; set; } = [];

    /// <summary>How the card is engaged (resolve-on-draw vs kept; forced vs choice; whose turn) (§5).</summary>
    public CardConditionType ConditionType { get; set; } = CardConditionType.None;

    /// <summary>True when the card is held until a trigger fires, i.e. any non-<see cref="CardConditionType.None"/> condition (§5).</summary>
    [JsonIgnore]
    public bool IsKeepUntilNeeded => ConditionType != CardConditionType.None;
    
    /// <summary>Whether the card suppresses the default action of the board space</summary>
    public bool SuppressDefault { get; set; }

    /// <summary>Parameterless constructor for serialisation.</summary>
    public CardModel()
    {
    }

    /// <summary>
    /// Copy constructor for the working-copy clone. Copies the per-instance fields and shares the
    /// immutable <see cref="Groups"/>/<see cref="Conditions"/> definition references rather than
    /// deep-copying the action tree every turn (see the note below).
    /// </summary>
    public CardModel(CardModel model)
    {
        CardId = model.CardId;
        CardText = model.CardText;
        CardType = model.CardType; 
        ConditionType = model.ConditionType;

        // Groups / Conditions are immutable card-definition data (fixed for the game's
        // life), so the working-copy clone shares the references rather than deep-copying
        // the whole action tree every turn. If a per-instance mutable field is ever added
        // (e.g. a charge counter), deep-copy that field — not this static content.
        Groups = model.Groups;
        Conditions = model.Conditions;
    }
}