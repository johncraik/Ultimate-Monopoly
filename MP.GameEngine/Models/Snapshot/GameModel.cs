using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Models.Snapshot;

public class GameModel
{
    /// <summary>
    /// This stores information about the game that can be traced back to database models
    /// </summary>
    public GameMetadata Metadata { get; set; }

    public bool ReserveRuleActive { get; set; }
    public uint FreeParkingAmount { get; set; }
    
    /// <summary>
    /// The array of players in the game
    /// </summary>
    public List<PlayerModel> Players { get; set; } = [];

    /// <summary>
    /// Properties owned by the bank and available for purchase
    /// </summary>
    public List<PropertyModel> Properties { get; set; } = [];

    /// <summary>
    /// Card decks (for each card type) that are not owned by any player
    /// </summary>
    public CardListModel CardDecks { get; set; } = new();
    
}