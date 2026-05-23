namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Empty response to an <see cref="PromptTypes.AcknowledgePrompt"/>. The value
/// is the pause itself — submitting this response unblocks the engine without
/// carrying any decision payload. The <see cref="PromptResponse.PromptId"/>
/// inherited from the base is the only field that matters.
/// </summary>
public sealed class AcknowledgeResponse : PromptResponse
{
}
