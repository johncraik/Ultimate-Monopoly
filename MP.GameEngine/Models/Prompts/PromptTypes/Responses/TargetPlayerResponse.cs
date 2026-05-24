namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of a <see cref="PromptTypes.TargetPlayerPrompt"/>. Carries the
/// selected player ids. The validator enforces that the list length equals
/// the prompt's <see cref="PromptTypes.TargetPlayerPrompt.Count"/>, that every
/// id is in the prompt's eligible set, and that there are no duplicates.
/// </summary>
public sealed class TargetPlayerResponse : PromptResponse
{
    public IReadOnlyList<string> SelectedPlayerIds { get; init; } = [];
}