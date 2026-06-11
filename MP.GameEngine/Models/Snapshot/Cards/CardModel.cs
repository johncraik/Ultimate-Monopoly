using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.Snapshot.Cards;

public class CardModel
{
    public string CardId { get; set; }  //TODO: make a GUID helper, so re-imported cards get same GUID as card in play
    public string CardText { get; set; }
    
    public CardType CardType { get; set; }
    
    public CardModel()
    {
    }

    public CardModel(CardModel model)
    {
        CardId = model.CardId;
        CardText = model.CardText;
        CardType = model.CardType;
    }
}