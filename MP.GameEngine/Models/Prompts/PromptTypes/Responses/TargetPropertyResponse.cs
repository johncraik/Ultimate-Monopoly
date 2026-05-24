namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of a <see cref="PromptTypes.TargetPropertyPrompt"/>. Carries
/// the selected property board indexes. The validator enforces that the
/// list length equals the prompt's
/// <see cref="PromptTypes.TargetPropertyPrompt.Count"/>, that every index is
/// in the prompt's eligible set, and that there are no duplicates.
/// </summary>
public sealed class TargetPropertyResponse : PromptResponse
{
    public IReadOnlyList<ushort> SelectedBoardIndexes { get; init; } = [];
}