using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.EventReceipts;

public class CardPlayedReceipt : EventReceipt
{
    public CardType CardType { get; init; }
    
    //TODO When cards are designed and implemented
}