using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers.Cards;

namespace MP.GameEngine.Models.Snapshot.Cards;

/// <summary>
/// The per-type card decks held on the game model — one <see cref="Queue{T}"/> of <b>card ids</b> per
/// <see cref="CardType"/>; the full card definitions live in the card cache and are resolved on draw
/// (<see cref="Take"/>). The queue order <i>is</i> the deck: drawing dequeues the front, returning
/// enqueues the back, and the id order persists in the snapshot so draws replay deterministically
/// (cards-design.md §9) while keeping the snapshot lean. Use <see cref="Take"/> / <see cref="HandBack"/>.
/// </summary>
public class CardListModel
{
    /// <summary>
    /// Chance card list
    /// </summary>
    public Queue<string> ChanceCards { get; set; } = [];
    internal void PopulateChanceCards(IEnumerable<CardModel> cards)
        => ChanceCards = new Queue<string>(cards.Select(c => c.CardId));
    
    
    /// <summary>
    /// Community chest card list
    /// </summary>
    public Queue<string> CommunityChestCards { get; set; } = [];
    internal void PopulateComChestCards(IEnumerable<CardModel> cards)
        => CommunityChestCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Percentage chance list
    /// </summary>
    public Queue<string> PercentChanceCards { get; set; } = [];
    internal void PopulatePercentChanceCards(IEnumerable<CardModel> cards)
        => PercentChanceCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Percentage community chest list
    /// </summary>
    public Queue<string> PercentCommunityChestCards { get; set; } = [];
    internal void PopulatePercentComChestCards(IEnumerable<CardModel> cards)
        => PercentCommunityChestCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Third cards list
    /// </summary>
    public Queue<string> ThirdCards { get; set; } = [];
    internal void PopulateThirdCards(IEnumerable<CardModel> cards)
        => ThirdCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Double cards list
    /// </summary>
    public Queue<string> DoubleCards { get; set; } = [];
    internal void PopulateDoubleCards(IEnumerable<CardModel> cards)
        => DoubleCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Triple cards list
    /// </summary>
    public Queue<string> TripleCards { get; set; } = [];
    internal void PopulateTripleCards(IEnumerable<CardModel> cards)
        => TripleCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Tax cards list
    /// </summary>
    public Queue<string> TaxCards { get; set; } = [];
    internal void PopulateTaxCards(IEnumerable<CardModel> cards)
        => TaxCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Go cards list
    /// </summary>
    public Queue<string> GoCards { get; set; } = [];
    internal void PopulateGoCards(IEnumerable<CardModel> cards)
        => GoCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Just visiting cards list
    /// </summary>
    public Queue<string> JustVisitingCards { get; set; } = [];
    internal void PopulateJustVisitingCards(IEnumerable<CardModel> cards)
        => JustVisitingCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Free parking cards list
    /// </summary>
    public Queue<string> FreeParkingCards { get; set; } = [];
    internal void PopulateFreeParkingCards(IEnumerable<CardModel> cards)
        => FreeParkingCards = new Queue<string>(cards.Select(c => c.CardId));

    /// <summary>
    /// Go to jail cards list
    /// </summary>
    public Queue<string> GoToJailCards { get; set; } = [];
    internal void PopulateGoToJailCards(IEnumerable<CardModel> cards)
        => GoToJailCards = new Queue<string>(cards.Select(c => c.CardId));
    
    
    /// <summary>Parameterless constructor for serialisation (empty decks).</summary>
    public CardListModel()
    {
    }


    /// <summary>Builds the shuffled per-type decks (as card-id queues) from a master card list (cards-design.md §9).</summary>
    /// <param name="cards">The full card list to deal into per-type decks.</param>
    // Chains to the copy ctor: BuildCardDecks returns a populated CardListModel, which the copy
    // ctor copies into this. (An expression-bodied `=> BuildCardDecks(cards)` would discard
    // the result and leave the decks empty.)
    public CardListModel(IEnumerable<CardModel> cards)
        : this(CardDeckHelper.BuildCardDecks(cards))
    {
    }


    /// <summary>Copy constructor for the working-copy clone — copies every deck's card-id queue.</summary>
    public CardListModel(CardListModel model)
    {
        ChanceCards = new Queue<string>(model.ChanceCards);
        CommunityChestCards = new Queue<string>(model.CommunityChestCards);
        
        PercentChanceCards = new Queue<string>(model.PercentChanceCards);
        PercentCommunityChestCards = new Queue<string>(model.PercentCommunityChestCards);
        ThirdCards = new Queue<string>(model.ThirdCards);
        
        DoubleCards = new Queue<string>(model.DoubleCards);
        TripleCards = new Queue<string>(model.TripleCards);
        
        TaxCards = new Queue<string>(model.TaxCards);
        
        GoCards = new Queue<string>(model.GoCards);
        JustVisitingCards = new Queue<string>(model.JustVisitingCards);
        FreeParkingCards = new Queue<string>(model.FreeParkingCards);
        GoToJailCards = new Queue<string>(model.GoToJailCards);
    }


    /// <summary>Returns the live deck queue for <paramref name="cardType"/> (the shared backing of <see cref="Has"/>/<see cref="Take"/>/<see cref="HandBack"/>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">An unknown <see cref="CardType"/> was passed.</exception>
    private Queue<string> DeckFor(CardType cardType)
      => cardType switch
      {
          CardType.Chance              => ChanceCards,                                                                                                                                                                                       
          CardType.ComChest            => CommunityChestCards,                                                                                                                                                                               
          CardType.PercentageChance       => PercentChanceCards,                                                                                                                                                                                
          CardType.PercentageComChest  => PercentCommunityChestCards,                                                                                                                                                                        
          CardType.Third               => ThirdCards,                                                                                                                                                                                        
          CardType.Double              => DoubleCards,                                                                                                                                                                                       
          CardType.Triple              => TripleCards,                                                                                                                                                                                       
          CardType.Tax                 => TaxCards,                                                                                                                                                                                          
          CardType.Go                  => GoCards,                                                                                                                                                                                           
          CardType.JustVisiting        => JustVisitingCards,                                                                                                                                                                                 
          CardType.FreeParking         => FreeParkingCards,                                                                                                                                                                                  
          CardType.GoToJail            => GoToJailCards,                                                                                                                                                                                     
          _ => throw new ArgumentOutOfRangeException(nameof(cardType), cardType, null)                                                                                                                                                       
      };


    /// <summary>Whether <paramref name="cardType"/>'s deck has any card ids left to draw.</summary>
    private bool Has(CardType cardType) => DeckFor(cardType).Count > 0;

    /// <summary>
    /// Draws the next card id off the front of <paramref name="cardType"/>'s deck and resolves it via
    /// <paramref name="cache"/> to a <b>fresh <see cref="CardModel"/> clone</b> — never the shared cached
    /// instance, so the returned card can be mutated and held without corrupting the cache or other games.
    /// Returns null if the deck is empty.
    /// </summary>
    public async Task<CardModel?> Take(ICardCacheService cache, CardType cardType)
    {
        var id = Has(cardType) ? DeckFor(cardType).Dequeue() : null;
        if (string.IsNullOrEmpty(id)) return null;

        var sharedCardModel = await cache.GetCard(id);
        return sharedCardModel == null ? null : new CardModel(sharedCardModel);
    }

    /// <summary>Returns a spent/played card to the back of its type's deck by enqueuing its id (cards-design.md §9.4).</summary>
    public void HandBack(CardType cardType, CardModel c) => DeckFor(cardType).Enqueue(c.CardId);
}