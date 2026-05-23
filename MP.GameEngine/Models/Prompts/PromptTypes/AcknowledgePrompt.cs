using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// A single-OK notification prompt the engine emits when the rules force an
/// outcome the player should be told about before it happens (e.g. "You cannot
/// afford this property — an auction will begin"). The engine pauses until the
/// target acknowledges. See <c>design-docs/choice-events.md</c> §3 case 1.
/// </summary>
/// <remarks>
/// Set <see cref="Prompt.Timeout"/> + <see cref="Prompt.DefaultResponse"/> on
/// construction if the engine wants the prompt to auto-dismiss after a delay.
/// </remarks>
public sealed class AcknowledgePrompt : Prompt<AcknowledgeResponse>
{
    /// <summary>The player being notified — the named target who taps OK.</summary>
    public string PlayerId { get; init; } = "";

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}
