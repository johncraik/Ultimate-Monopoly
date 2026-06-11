using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Helpers.Cards;

public static class CardDeckHelper
{
    private static void Shuffle(this List<CardModel> list)
    {
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = Random.Shared.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public static CardListModel BuildCardDecks(IEnumerable<CardModel> cards)
    {
        var groupedCards = cards
            .GroupBy(c => c.CardType)
            .ToList();

        var model = new CardListModel();
        foreach (var gc in groupedCards)
        {
            //Get the list of cards and shuffle them
            var (type, cs) = (gc.Key, gc.ToList());
            cs.Shuffle();
            
            switch (type)
            {
                case CardType.Chance:
                    model.ChanceCards = new Queue<CardModel>(cs);
                    break;
                case CardType.ComChest:
                    model.CommunityChestCards = new Queue<CardModel>(cs);
                    break;
                case CardType.PercentChance:
                    model.PercentChanceCards = new Queue<CardModel>(cs);
                    break;
                case CardType.PercentageComChest:
                    model.PercentCommunityChestCards = new Queue<CardModel>(cs);
                    break;
                case CardType.Third:
                    model.ThirdCards = new Queue<CardModel>(cs);
                    break;
                case CardType.Double:
                    model.DoubleCards = new Queue<CardModel>(cs);
                    break;
                case CardType.Triple:
                    model.TripleCards = new Queue<CardModel>(cs);
                    break;
                case CardType.Tax:
                    model.TaxCards = new Queue<CardModel>(cs);
                    break;
                case CardType.Go:
                    model.GoCards = new Queue<CardModel>(cs);
                    break;
                case CardType.JustVisiting:
                    model.JustVisitingCards = new Queue<CardModel>(cs);
                    break;
                case CardType.FreeParking:
                    model.FreeParkingCards = new Queue<CardModel>(cs);
                    break;
                case CardType.GoToJail:
                    model.GoToJailCards = new Queue<CardModel>(cs);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        
        return model;
    }
}