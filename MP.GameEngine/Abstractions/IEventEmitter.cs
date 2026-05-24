using MP.GameEngine.Models.EventReceipts;

namespace MP.GameEngine.Abstractions;

/// <summary>
/// The seam rule services use to emit <see cref="EventReceipt"/>s. Pairs
/// with <see cref="IPromptProvider"/> as the engine's output channel —
/// prompts ask the player for input, receipts record what happened. See
/// <c>design-docs/event-receipts.md</c> §5.
/// </summary>
public interface IEventEmitter
{
    /// <summary>
    /// Records a state change. The producer constructs the receipt and sets
    /// its receipt-specific fields plus <see cref="EventReceipt.PlayerId"/>;
    /// the framework backfills <see cref="EventReceipt.TurnNumber"/> and
    /// <see cref="EventReceipt.SequenceIndex"/>.
    /// </summary>
    /// <param name="receipt">The receipt to emit. Must be emitted only after the corresponding state mutation succeeds.</param>
    void Emit(EventReceipt receipt);
}
