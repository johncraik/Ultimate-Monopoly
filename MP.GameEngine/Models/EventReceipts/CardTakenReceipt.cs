using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.EventReceipts;

public class CardTakenReceipt : EventReceipt
{
    public CardType CardType { get; set; }
    
    //TODO When cards are designed and implemented
}