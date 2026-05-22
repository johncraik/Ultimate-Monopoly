using System.Text.Json.Serialization;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Models.Snapshot;

public class PlayerModel
{
    //Player metadata linking to database models
    public string PlayerId { get; set; }
    public ushort OrderId { get; set; }
    public ushort Dice1 { get; set; }
    public ushort Dice2 { get; set; }
    
    public bool HasPassedInitialGo { get; set; }
    public uint Money { get; set; }
    public ushort BoardIndex { get; set; }
    public PlayerDirection Direction { get; set; }
    
    public ushort DoublesInRow { get; set; }
    public ushort TriplesInRow { get; set; }
    
    public uint TripleBonus { get; set; }
    public uint JailCost { get; set; }
    
    public ushort TurnsToMiss { get; set; }
    [JsonIgnore]
    public bool MissNextTurn => TurnsToMiss > 0;
    
    [JsonIgnore]
    public bool IsInJail => BoardIndex == IndexHelper.JailSpace;
    public ushort JailTurnCounter { get; set; }
    public ushort? MaxJailTurnsOverride { get; set; }
    
    public bool IsBankrupt { get; set; }

    /// <summary>
    /// Cards owned by the player (keep until needed/played upon condition)
    /// </summary>
    public List<CardModel> Cards { get; set; } = [];

    /// <summary>
    /// All loans taken out by the player, including those that have been paid off
    /// </summary>
    public List<LoanModel> Loans { get; set; } = [];
    
    
    public List<PropertySet> FPHandedInSets { get; set; } = [];
}