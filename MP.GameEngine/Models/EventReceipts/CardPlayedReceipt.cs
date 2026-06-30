using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Models.EventReceipts;

public class CardPlayedReceipt : EventReceipt
{
    public string CardId { get; init; } = "";
    public CardType CardType { get; init; }
    public CardTrigger AllTriggers { get; init; }
    public CardConditionType ConditionType { get; init; }
    
    public string RawText { get; init; } = "";
    public string DisplayText { get; init; } = "";
    
    public string ChosenGroupId { get; init; } = "";
    public string? GroupRawText { get; init; }
    public string? GroupDisplayText { get; init; }
    public ushort NumberOfActions { get; init; }
    
    public bool IsImmunity { get; init; }

    /// <summary>True when the chosen group frees the holder from jail (a Get-Out-of-Jail-Free
    /// <see cref="JailKind.Release"/> action) — lets jail stats count card-driven jail exits (M-06).</summary>
    public bool IsJailRelease { get; init; }

    //NOPE cards coming in feature UPDATE
    //public bool IsNope { get; init; }

    public CardPlayedReceipt()
    {
    }

    public CardPlayedReceipt(CardModel card, GameCacheModel cache, string playerId)
    {
        PlayerId = playerId;
        CardId = card.CardId;
        CardType = card.CardType;
        
        try
        {
            var triggers = card.Conditions
                .Select(c => c.Trigger)
                .Aggregate(0, (current, trigger) => current | (int)trigger);
            AllTriggers = (CardTrigger)triggers;
        } catch { AllTriggers = CardTrigger.None; }
        
        ConditionType = card.ConditionType;

        RawText = card.CardText;
        DisplayText = card.GetDisplayText(cache, playerId);
        
        var group = card.Groups.FirstOrDefault(g => g.IsChosenGroup);
        if(group != null)
        {
            ChosenGroupId = group.GroupId;
            GroupRawText = group.GroupText;
            GroupDisplayText = group.GetDisplayText(cache, playerId);
            NumberOfActions = (ushort)group.Actions.Count;
            
            IsImmunity = group.Actions.Any(a => a is ImmunityAction);
            IsJailRelease = group.Actions.OfType<JailAction>().Any(j => j.Kind == JailKind.Release);
        }
    }
}