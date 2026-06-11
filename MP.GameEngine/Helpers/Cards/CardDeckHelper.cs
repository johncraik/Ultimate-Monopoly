using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Helpers.Cards;

/// <summary>
/// Builds the per-type card decks at game creation: groups the master card list by
/// <see cref="CardType"/>, shuffles each group once, and loads it into the matching
/// <see cref="CardListModel"/> queue. The shuffled order <i>is</i> the deck and persists in the
/// snapshot, so draws replay deterministically (cards-design.md §9).
/// </summary>
public static class CardDeckHelper
{
    /// <summary>In-place Fisher-Yates shuffle using <see cref="Random.Shared"/>.</summary>
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

    /// <summary>
    /// Groups <paramref name="cards"/> by <see cref="CardType"/>, shuffles each group, and returns
    /// a <see cref="CardListModel"/> with every type's deck loaded.
    /// </summary>
    /// <param name="cards">The full master card list to deal into per-type decks.</param>
    /// <returns>The populated, shuffled per-type decks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An unknown <see cref="CardType"/> was encountered.</exception>
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
                case CardType.PercentageChance:
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