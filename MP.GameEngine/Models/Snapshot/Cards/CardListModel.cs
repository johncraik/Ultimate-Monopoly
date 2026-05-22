namespace MP.GameEngine.Models.Snapshot.Cards;

public class CardListModel
{
    /// <summary>
    /// Chance card list
    /// </summary>
    public List<CardModel> ChanceCards { get; set; } = [];

    /// <summary>
    /// Community chest card list
    /// </summary>
    public List<CardModel> CommunityChestCards { get; set; } = [];

    /// <summary>
    /// Percentage chance list
    /// </summary>
    public List<CardModel> PercentChanceCards { get; set; } = [];

    /// <summary>
    /// Percentage community chest list
    /// </summary>
    public List<CardModel> PercentCommunityChestCards { get; set; } = [];

    /// <summary>
    /// Third cards list
    /// </summary>
    public List<CardModel> ThirdCards { get; set; } = [];

    /// <summary>
    /// Double cards list
    /// </summary>
    public List<CardModel> DoubleCards { get; set; } = [];

    /// <summary>
    /// Triple cards list
    /// </summary>
    public List<CardModel> TripleCards { get; set; } = [];

    /// <summary>
    /// Tax cards list
    /// </summary>
    public List<CardModel> TaxCards { get; set; } = [];

    /// <summary>
    /// Go cards list
    /// </summary>
    public List<CardModel> GoCards { get; set; } = [];

    /// <summary>
    /// Just visiting cards list
    /// </summary>
    public List<CardModel> JustVisitingCards { get; set; } = [];

    /// <summary>
    /// Free parking cards list
    /// </summary>
    public List<CardModel> FreeParkingCards { get; set; } = [];

    /// <summary>
    /// Go to jail cards list
    /// </summary>
    public List<CardModel> GoToJailCards { get; set; } = [];
}