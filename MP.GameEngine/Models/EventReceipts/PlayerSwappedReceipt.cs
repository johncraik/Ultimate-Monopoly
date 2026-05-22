using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.EventReceipts;

public class PlayerSwappedReceipt : EventReceipt
{
    public ushort InitialPlayerBoardIndex { get; set; }
    public ushort FinalPlayerBoardIndex { get; set; }
    
    //The "Swapped Player" is the player that moves (swaps with) the player who made the swap
    //Eg; player A chose to swap with player B, the "Swapped Player" is player B
    public string SwappedPlayerId { get; set; }
    
    [JsonIgnore]
    public ushort SwappedPlayerBoardIndex => InitialPlayerBoardIndex;
}