using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.EventReceipts;

public class DiceRollReceipt : EventReceipt
{
    public ushort Dice1 { get; init; }
    public ushort? Dice2 { get; init; }
    public ushort? Dice3 { get; init; }
    
    [JsonIgnore]
    public bool IsTurnRoll => Dice2 != null && Dice3 != null;
}