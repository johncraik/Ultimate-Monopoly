namespace MP.GameEngine.Enums;

/// <summary>
/// Categorical axis for <see cref="Models.EventReceipts.FinancialTransactionReceipt"/>.
/// Every money movement carries a reason — stats group by reason without
/// re-implementing rule logic. See <c>design-docs/event-receipts.md</c> §4.
/// </summary>
public enum FinancialReason
{
    /// <summary>Rent paid on a landed-space (uses <see cref="Models.EventReceipts.FinancialTransactionReceipt.SourcePropertyId"/>).</summary>
    Rent,

    /// <summary>Tax space payout.</summary>
    Tax,

    /// <summary>Rule or card-driven fine.</summary>
    Fine,

    /// <summary>Passing or landing on GO.</summary>
    GoBonus,

    /// <summary>Paying out of jail.</summary>
    JailFee,

    /// <summary>Money paid into the Free Parking pot.</summary>
    FreeParkingPay,

    /// <summary>Money taken out of the Free Parking pot.</summary>
    FreeParkingTake,

    /// <summary>Money received from a loan being taken out.</summary>
    LoanTake,

    /// <summary>Money paid towards a loan repayment.</summary>
    LoanRepay,

    /// <summary>Buying a property (uses <see cref="Models.EventReceipts.FinancialTransactionReceipt.SourcePropertyId"/>).</summary>
    Purchase,

    /// <summary>Winning an auction (uses <see cref="Models.EventReceipts.FinancialTransactionReceipt.SourcePropertyId"/>).</summary>
    Auction,

    /// <summary>Building a house or hotel (uses <see cref="Models.EventReceipts.FinancialTransactionReceipt.SourcePropertyId"/>).</summary>
    Build,

    /// <summary>Selling a building back to the bank (uses <see cref="Models.EventReceipts.FinancialTransactionReceipt.SourcePropertyId"/>).</summary>
    Sell,

    /// <summary>Mortgaging — money in (uses <see cref="Models.EventReceipts.FinancialTransactionReceipt.SourcePropertyId"/>).</summary>
    Mortgage,
    
    /// <summary>Fee paid when passing go - money out</summary>
    MortgageFee,

    /// <summary>Unmortgaging — money out (uses <see cref="Models.EventReceipts.FinancialTransactionReceipt.SourcePropertyId"/>).</summary>
    Unmortgage,

    /// <summary>Card-driven money in.</summary>
    CardPayout,

    /// <summary>Card-driven money out.</summary>
    CardCharge,

    /// <summary>Money component of a player-to-player deal.</summary>
    Deal
}
