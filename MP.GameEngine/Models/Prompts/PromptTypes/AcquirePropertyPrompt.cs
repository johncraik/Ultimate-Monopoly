using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Asks the lander whether they want to acquire the property they have landed
/// on. "Acquire" covers both a standard purchase and a reservation under the
/// reserve rule (<c>game-rules.md</c> Reserved Properties) — which of the two
/// is happening is engine state, not framework state. The prompt's job is the
/// binary yes/no; the engine that creates the prompt knows which path the
/// "yes" leads to and branches accordingly when it receives the response.
/// </summary>
/// <remarks>
/// Affordability is gated before this prompt opens. If the lander cannot
/// afford the offered action (full price for buy, half price for reserve),
/// the engine emits an <see cref="AcknowledgePrompt"/> ("can't afford,
/// auction begins") and skips this prompt entirely.
/// </remarks>
public sealed class AcquirePropertyPrompt : Prompt<AcquirePropertyResponse>
{
    /// <summary>
    /// The property's <see cref="Snapshot.PropertyModel.BoardIndex"/>. The set
    /// and colour can be resolved through
    /// <see cref="Helpers.PropertySetHelper.ResolveSet(ushort)"/>.
    /// </summary>
    public ushort BoardIndex { get; init; }

    /// <summary>
    /// What the player would pay for the offered action — full price for a
    /// standard buy, half price for a reserve. The engine that creates the
    /// prompt computes this; the framework does not interpret it beyond
    /// passing it on.
    /// </summary>
    public uint Cost { get; init; }

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}
