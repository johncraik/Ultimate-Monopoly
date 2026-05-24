namespace MP.GameEngine.Models.EventReceipts;

/// <summary>
/// Records the subject player being sent to jail — for any cause (3 doubles
/// in a row, landing on Go-To-Jail, card effect). Semantic receipt that
/// drives the "times in jail" stat without making the stats projection
/// infer it from a <see cref="PlayerMovedReceipt"/> ending at the jail
/// space. See <c>design-docs/event-receipts.md</c> §3.3.
/// </summary>
public class PlayerEnteredJailReceipt : EventReceipt
{
}
