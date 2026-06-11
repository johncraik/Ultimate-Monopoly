using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers.Cards;

namespace MP.GameEngine.Models.Snapshot.Cards;

public class CardListModel
{
    /// <summary>
    /// Chance card list
    /// </summary>
    public Queue<CardModel> ChanceCards { get; set; } = [];

    /// <summary>
    /// Community chest card list
    /// </summary>
    public Queue<CardModel> CommunityChestCards { get; set; } = [];

    /// <summary>
    /// Percentage chance list
    /// </summary>
    public Queue<CardModel> PercentChanceCards { get; set; } = [];

    /// <summary>
    /// Percentage community chest list
    /// </summary>
    public Queue<CardModel> PercentCommunityChestCards { get; set; } = [];

    /// <summary>
    /// Third cards list
    /// </summary>
    public Queue<CardModel> ThirdCards { get; set; } = [];

    /// <summary>
    /// Double cards list
    /// </summary>
    public Queue<CardModel> DoubleCards { get; set; } = [];

    /// <summary>
    /// Triple cards list
    /// </summary>
    public Queue<CardModel> TripleCards { get; set; } = [];

    /// <summary>
    /// Tax cards list
    /// </summary>
    public Queue<CardModel> TaxCards { get; set; } = [];

    /// <summary>
    /// Go cards list
    /// </summary>
    public Queue<CardModel> GoCards { get; set; } = [];

    /// <summary>
    /// Just visiting cards list
    /// </summary>
    public Queue<CardModel> JustVisitingCards { get; set; } = [];

    /// <summary>
    /// Free parking cards list
    /// </summary>
    public Queue<CardModel> FreeParkingCards { get; set; } = [];

    /// <summary>
    /// Go to jail cards list
    /// </summary>
    public Queue<CardModel> GoToJailCards { get; set; } = [];

    public CardListModel()
    {
    }

    
    public CardListModel(IEnumerable<CardModel> cards)
        => CardDeckHelper.BuildCardDecks(cards);
    

    public CardListModel(CardListModel model)
    {
        ChanceCards = new Queue<CardModel>(model.ChanceCards.Select(c => new CardModel(c)));
        CommunityChestCards = new Queue<CardModel>(model.CommunityChestCards.Select(c => new CardModel(c)));
        
        PercentChanceCards = new Queue<CardModel>(model.PercentChanceCards.Select(c => new CardModel(c)));
        PercentCommunityChestCards = new Queue<CardModel>(model.PercentCommunityChestCards.Select(c => new CardModel(c)));
        ThirdCards = new Queue<CardModel>(model.ThirdCards.Select(c => new CardModel(c)));
        
        DoubleCards = new Queue<CardModel>(model.DoubleCards.Select(c => new CardModel(c)));
        TripleCards = new Queue<CardModel>(model.TripleCards.Select(c => new CardModel(c)));
        
        TaxCards = new Queue<CardModel>(model.TaxCards.Select(c => new CardModel(c)));
        
        GoCards = new Queue<CardModel>(model.GoCards.Select(c => new CardModel(c)));
        JustVisitingCards = new Queue<CardModel>(model.JustVisitingCards.Select(c => new CardModel(c)));
        FreeParkingCards = new Queue<CardModel>(model.FreeParkingCards.Select(c => new CardModel(c)));
        GoToJailCards = new Queue<CardModel>(model.GoToJailCards.Select(c => new CardModel(c)));
    }


    private Queue<CardModel> DeckFor(CardType cardType)                                                                                                                                                                                        
      => cardType switch                                                                                                                                                                                                                     
      {                                                                                                                                                                                                                                      
          CardType.Chance              => ChanceCards,                                                                                                                                                                                       
          CardType.ComChest            => CommunityChestCards,                                                                                                                                                                               
          CardType.PercentChance       => PercentChanceCards,                                                                                                                                                                                
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
    
    public bool Has(CardType cardType) => DeckFor(cardType).Count > 0;
    public CardModel? Take(CardType cardType) => Has(cardType) ? DeckFor(cardType).Dequeue() : null;
    public void HandBack(CardType cardType, CardModel c) => DeckFor(cardType).Enqueue(c);
    
    
    public bool HasChanceCards => ChanceCards.Count > 0;
    public CardModel? TakeChance() => HasChanceCards ? ChanceCards.Dequeue() : null;
    public void HandBackChance(CardModel c) => ChanceCards.Enqueue(c);
    
    public bool HasCommunityChestCards => CommunityChestCards.Count > 0;
    public CardModel? TakeCommunityChest() => HasCommunityChestCards ? CommunityChestCards.Dequeue() : null;
    public void HandBackCommunityChest(CardModel c) => CommunityChestCards.Enqueue(c);
    
    public bool HasPercentChanceCards => PercentChanceCards.Count > 0;
    public CardModel? TakePercentChance() => HasPercentChanceCards ? PercentChanceCards.Dequeue() : null;
    public void HandBackPercentChance(CardModel c) => PercentChanceCards.Enqueue(c);
    
    public bool HasPercentCommunityChestCards => PercentCommunityChestCards.Count > 0;
    public CardModel? TakePercentCommunityChest() => HasPercentCommunityChestCards ? PercentCommunityChestCards.Dequeue() : null;
    public void HandBackPercentCommunityChest(CardModel c) => PercentCommunityChestCards.Enqueue(c);
    
    public bool HasThirdCards => ThirdCards.Count > 0;
    public CardModel? TakeThird() => HasThirdCards ? ThirdCards.Dequeue() : null;
    public void HandBackThird(CardModel c) => ThirdCards.Enqueue(c);
    
    public bool HasDoubleCards => DoubleCards.Count > 0;
    public CardModel? TakeDouble() => HasDoubleCards ? DoubleCards.Dequeue() : null;
    public void HandBackDouble(CardModel c) => DoubleCards.Enqueue(c);
    
    public bool HasTripleCards => TripleCards.Count > 0;
    public CardModel? TakeTriple() => HasTripleCards ? TripleCards.Dequeue() : null;
    public void HandBackTriple(CardModel c) => TripleCards.Enqueue(c);
    
    public bool HasTaxCards => TaxCards.Count > 0;
    public CardModel? TakeTax() => HasTaxCards ? TaxCards.Dequeue() : null;
    public void HandBackTax(CardModel c) => TaxCards.Enqueue(c);
    
    public bool HasGoCards => GoCards.Count > 0;
    public CardModel? TakeGo() => HasGoCards ? GoCards.Dequeue() : null;
    public void HandBackGo(CardModel c) => GoCards.Enqueue(c);
    
    public bool HasJustVisitingCards => JustVisitingCards.Count > 0;
    public CardModel? TakeJustVisiting() => HasJustVisitingCards ? JustVisitingCards.Dequeue() : null;
    public void HandBackJustVisiting(CardModel c) => JustVisitingCards.Enqueue(c);
    
    public bool HasFreeParkingCards => FreeParkingCards.Count > 0;
    public CardModel? TakeFreeParking() => HasFreeParkingCards ? FreeParkingCards.Dequeue() : null;
    public void HandBackFreeParking(CardModel c) => FreeParkingCards.Enqueue(c);
    
    public bool HasGoToJailCards => GoToJailCards.Count > 0;
    public CardModel? TakeGoToJail() => HasGoToJailCards ? GoToJailCards.Dequeue() : null;
    public void HandBackGoToJail(CardModel c) => GoToJailCards.Enqueue(c);
}