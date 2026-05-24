using MP.GameEngine.Enums;

namespace MP.GameEngine.Models.EventReceipts;

/// <summary>
/// Records a money movement. From the subject player's perspective:
/// positive <see cref="Amount"/> = received, negative = paid. The categorical
/// <see cref="Reason"/> axis lets the stats projection aggregate without
/// re-implementing rule logic; <see cref="SourcePropertyId"/> attaches
/// property attribution for the reasons where that matters (rent, purchase,
/// build, etc.). See <c>design-docs/event-receipts.md</c> §4.
/// </summary>
public class FinancialTransactionReceipt : EventReceipt
{
    /// <summary>Signed amount from the subject player's perspective — negative = pay, positive = receive.</summary>
    public long Amount { get; init; }

    /// <summary>Categorical reason for the movement. Stats group by this.</summary>
    public FinancialReason Reason { get; init; }

    /// <summary>The other side of the transaction (source when receiving, destination when paying).</summary>
    public TransactionDestination Destination { get; init; }

    /// <summary>The counterparty player id when <see cref="Destination"/> is <see cref="TransactionDestination.Player"/>; null otherwise.</summary>
    public string? DestinationPlayerId { get; init; }

    /// <summary>
    /// The <see cref="Snapshot.PropertyModel.BoardIndex"/> the transaction
    /// relates to, when relevant — set for rent, purchase, auction, build,
    /// sell, mortgage, unmortgage. Null when the transaction has no property
    /// context (GO bonus, jail fee, tax, etc.).
    /// </summary>
    public ushort? SourcePropertyId { get; init; }
}
