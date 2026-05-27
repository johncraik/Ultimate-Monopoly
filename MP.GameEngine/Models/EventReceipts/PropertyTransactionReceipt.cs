using MP.GameEngine.Enums;

namespace MP.GameEngine.Models.EventReceipts;

public class PropertyTransactionReceipt : EventReceipt
{
    //Value = number of properties when SetsOnly is false, or number of sets when SetsOnly is true
    public int Value { get; init; }
    public bool SetsOnly { get; init; }
    
    //When value is positive, this becomes the source
    public TransactionCounterparty Counterparty { get; init; }
    public string? DestinationPlayerId { get; init; }
}