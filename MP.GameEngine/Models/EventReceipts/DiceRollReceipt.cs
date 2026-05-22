using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.EventReceipts;

public class DiceRollReceipt : EventReceipt
{
    public ushort Dice1 { get; set; }
    public ushort? Dice2 { get; set; }
    public ushort? Dice3 { get; set; }
    
    [JsonIgnore]
    public bool IsTurnRoll => Dice2 != null && Dice3 != null;
}