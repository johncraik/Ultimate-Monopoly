using MP.GameEngine.Enums.Cards;
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
    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);

    /// <summary>
    /// The deck a drawn card came from, when this acknowledge announces a card pick-up — drives
    /// card-type-flavoured styling on the front end. <c>null</c> for every other acknowledge
    /// (rule notifications, can't-afford, etc.), which render in the default secondary styling.
    /// </summary>
    public CardType? CardType { get; init; }
    public bool PlayingCard { get; init; }
}
