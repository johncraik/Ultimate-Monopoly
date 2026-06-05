using MP.GameEngine.Enums;

namespace MP.GameEngine.Models.EventReceipts;

/// <summary>
/// Records a property ownership change — a title moving between a player, the
/// bank, or the Free Parking pot. Ownership only; the money leg of an action (if
/// any) is a separate <see cref="FinancialTransactionReceipt"/>. The categorical
/// <see cref="Reason"/> axis lets the stats projection attribute moves (bought /
/// won / dealt / handed-in) without re-implementing rule logic. See
/// <c>design-docs/event-receipts.md</c> §3.2.
/// </summary>
public class PropertyTransferReceipt : EventReceipt
{
    //Value = number of properties when SetsOnly is false, or number of sets when SetsOnly is true
    public int Value { get; init; }
    public bool SetsOnly { get; init; }

    /// <summary>Categorical reason for the move. Stats group by this.</summary>
    public PropertyTransferReason Reason { get; init; }

    //When value is positive, this becomes the source
    public TransactionCounterparty Counterparty { get; init; }
    public string? CounterpartyPlayerId { get; init; }
}