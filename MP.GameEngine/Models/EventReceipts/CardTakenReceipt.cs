using JC.Core.Extensions;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Models.EventReceipts;

public class CardTakenReceipt : EventReceipt
{
    public string CardId { get; init; } = "";
    public CardType CardType { get; init; }
    public CardTrigger AllTriggers { get; init; }
    public CardConditionType ConditionType { get; init; }
    
    public string RawText { get; init; } = "";
    public string DisplayText { get; init; } = "";
    
    public ushort NumberOfGroups { get; init; }
    public ushort NumberOfActions { get; init; }
    public ushort NumberOfConditions { get; init; }
    public bool IsImmunity { get; init; }
    
    //NOPE cards coming in feature UPDATE
    //public bool IsNope { get; init; }

    public CardTakenReceipt()
    {
    }

    public CardTakenReceipt(CardModel card, GameCacheModel cache, string playerId)
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
        
        NumberOfGroups = (ushort)card.Groups.Count;
        NumberOfActions = (ushort)card.Groups.Sum(g => g.Actions.Count);
        NumberOfConditions = (ushort)card.Conditions.Count;

        IsImmunity = card.Groups.Any(g => g.Actions.Any(a => a is ImmunityAction));
    }
}