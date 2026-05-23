namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of an <see cref="PromptTypes.AcquirePropertyPrompt"/>.
/// <c>true</c> = take the property (the engine decides whether that means buy
/// or reserve based on game state); <c>false</c> = decline, which sends the
/// property to auction.
/// </summary>
public sealed class AcquirePropertyResponse : PromptResponse
{
    public bool Accept { get; init; }
}
