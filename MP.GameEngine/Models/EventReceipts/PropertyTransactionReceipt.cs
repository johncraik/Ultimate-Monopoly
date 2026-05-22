using MP.GameEngine.Enums;

namespace MP.GameEngine.Models.EventReceipts;

public class PropertyTransactionReceipt : EventReceipt
{
    //Value = number of properties when SetsOnly is false, or number of sets when SetsOnly is true
    public int Value { get; set; }
    public bool SetsOnly { get; set; }
    
    //When value is positive, this becomes the source
    public TransactionDestination Destination { get; set; }
    public string? DestinationPlayerId { get; set; }
}