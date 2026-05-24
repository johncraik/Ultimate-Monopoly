namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of a <see cref="PromptTypes.CardOptionPrompt"/>. Carries the
/// <see cref="PromptTypes.CardOption.Key"/> of the chosen option. The
/// validator enforces that the key matches one of the prompt's options.
/// </summary>
public sealed class CardOptionResponse : PromptResponse
{
    //TODO: This may change when card framework is implemented
    
    public string SelectedKey { get; init; } = "";
}