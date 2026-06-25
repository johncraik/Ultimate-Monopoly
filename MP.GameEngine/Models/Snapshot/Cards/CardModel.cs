using System.Text.Json.Serialization;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
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
    /// <summary>Raw card text with any unique tags to prevent duplicate text entries</summary>
    public string UniqueText { get; set; }
    
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
    public SuppressDefault SuppressDefault { get; set; } = new(SuppressDefaultType.None);

    /// <summary>
    /// The <see cref="Snapshot.TurnMetadata.TurnNumber"/> on which this card was drawn into a hand (null when
    /// not currently held). Gates <see cref="Enums.Cards.CardTrigger.OnNextMove"/>: an "after your next move"
    /// card must not fire on the very move that drew it, so it only becomes live once the turn number has
    /// advanced past the one it was acquired on.
    /// </summary>
    public uint? DrawnOnTurn { get; set; }



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
        UniqueText = model.UniqueText;
        CardText = model.CardText;
        CardType = model.CardType; 
        ConditionType = model.ConditionType;
        SuppressDefault = new SuppressDefault(model.SuppressDefault.Type());
        DrawnOnTurn = model.DrawnOnTurn;

        Groups = model.Groups.Select(g => new CardGroup(g)).ToList().AsReadOnly();
        Conditions = model.Conditions.Select(c => new CardCondition(c)).ToList().AsReadOnly();
    }

    public CardModel(CardModel model, PlayerCardInstance instance) 
        : this(model)
    {
        DrawnOnTurn = instance.DrawnOnTurn;
        
        var group = Groups.FirstOrDefault(x => x.GroupId == instance.ChosenGroupId);
        if(group == null) return;
        
        group.IsChosenGroup = true;
        group.TurnsRemaining = instance.TurnsRemaining;
    }



    public string GetDisplayText(GameCacheModel gameCache, string playerId)
    {
        var roundingRule = gameCache.RoundingRule;
        var playerCap = gameCache.Game.PlayerPercentCap(playerId);

        return Groups.Aggregate(CardText, (current, g)
            => current.FormatCardText(g, playerCap, roundingRule, false));
    }
}