using MP.GameEngine.Enums;

namespace MP.GameEngine.Models.EventReceipts;

public class FinancialTransactionReceipt : EventReceipt
{
    //Negatives are pay from PlayerId
    public long Amount { get; set; }
    
    //When amount is positive, this becomes the source
    public TransactionDestination Destination { get; set; }
    public string? DestinationPlayerId { get; set; }
}