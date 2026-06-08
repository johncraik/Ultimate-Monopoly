namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of a <see cref="PromptTypes.DealPrompt"/>. A bare accept/decline —
/// the deal contents are server-authored on the prompt and not re-supplied here.
/// There is no counter-offer: declining simply ends the deal.
/// See <c>design-docs/game-deals.md</c> §9, §13.
/// </summary>
public sealed class DealResponse : PromptResponse
{
    /// <summary><c>true</c> to accept the deal as proposed; <c>false</c> to decline.</summary>
    public bool Accept { get; init; }
}