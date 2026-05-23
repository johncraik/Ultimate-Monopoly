namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of an <see cref="InterruptibleWindowPrompt"/>. Carries one of
/// two action paths — see <see cref="InterruptAction"/>.
/// </summary>
public sealed class InterruptibleWindowResponse : PromptResponse
{
    public InterruptAction Action { get; init; }

    /// <summary>The player whose card is being played. Required when <see cref="Action"/> is <see cref="InterruptAction.PlayCard"/>; ignored otherwise.</summary>
    public string? PlayedByPlayerId { get; init; }

    /// <summary>The card being played. Required when <see cref="Action"/> is <see cref="InterruptAction.PlayCard"/>; ignored otherwise.</summary>
    public string? PlayedCardId { get; init; }
}

/// <summary>
/// The two ways an <see cref="InterruptibleWindowPrompt"/> can be closed.
/// </summary>
public enum InterruptAction
{
    /// <summary>No response card played. The host (tablet) tells the engine to proceed.</summary>
    Continue,

    /// <summary>An eligible holder played a response card. The engine records the play and chains a new window if anyone can respond to it.</summary>
    PlayCard
}
