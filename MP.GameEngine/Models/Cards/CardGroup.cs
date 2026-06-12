using MP.GameEngine.Helpers.Cards;

namespace MP.GameEngine.Models.Cards;

/// <summary>
/// One choosable option on a card (cards-design.md §2): its <see cref="Actions"/> are ANDed (all
/// happen), while a card's groups are ORed (the player picks one). A single-group card is a
/// non-choice; two-plus groups surface a <see cref="Models.Prompts.PromptTypes.CardOptionPrompt"/>.
/// </summary>
public class CardGroup
{
    /// <summary>Stable identity (GUID) — the key returned when this group is chosen.</summary>
    public string GroupId { get; set; }
    /// <summary>The slice of the card text describing this option.</summary>
    public string GroupText { get; set; }

    /// <summary>The actions applied (in order, all of them) when this group is chosen — at least one.</summary>
    public IReadOnlyList<CardAction> Actions { get; set; }
    
    public bool IsChosenGroup { get; set; }
    
    //Turns active is default value - DO NOT CHANGE VALUE
    public ushort? TurnsActive { get; set; }
    public ushort? TurnsRemaining { get; set; }
    
    
    public string GroupKey { get; set; }

    public string GetDisplayText(GameCacheModel gameCache, string playerId)
    {
        var roundingRule = gameCache.RoundingRule;
        var playerCap = gameCache.Game.PlayerPercentCap(playerId);

        return GroupText.FormatCardText(this, playerCap, roundingRule, true);
    }
}