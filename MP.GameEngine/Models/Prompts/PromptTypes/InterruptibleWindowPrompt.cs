using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// A window during which an in-flight card play can be interrupted by a card
/// another player holds (NOPE being the headline case). Resolves when the
/// host taps Continue, or when an eligible holder plays a response card.
/// See <c>design-docs/choice-events.md</c> §9.
/// </summary>
/// <remarks>
/// The engine opens this prompt only when at least one other player holds a
/// card that could respond — otherwise the play is applied directly with no
/// prompt. <see cref="Prompt.Timeout"/> is always <c>null</c>: the table
/// decides when the window closes.
/// </remarks>
public sealed class InterruptibleWindowPrompt : Prompt<InterruptibleWindowResponse>
{
    /// <summary>The set of (player, card) pairs that are valid response plays in this window.</summary>
    public IReadOnlyList<EligibleCardPlay> EligiblePlays { get; init; } = [];

    public override PromptTarget Target =>
        PromptTarget.Group(EligiblePlays.Select(e => e.PlayerId).Distinct());
}

/// <summary>
/// A single (player, card) pair the engine has determined could respond to
/// the action wrapped by an <see cref="InterruptibleWindowPrompt"/>.
/// </summary>
public sealed record EligibleCardPlay(
    string PlayerId,
    string CardId,
    string CardName);
