using MP.GameEngine.Models.Deals;

namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of a <see cref="PromptTypes.BuildDealPrompt"/>. Carries the full
/// <see cref="DealContents"/> the debtor constructed. The validator confirms
/// every property is drawn from the prompt's dealable sets (no duplicates) and
/// that each side's money is within that side's cash. The contents then ride on
/// the <see cref="PromptTypes.DealPrompt"/> sent to the creditor.
/// See <c>design-docs/game-deals.md</c> §13.
/// </summary>
public sealed class BuildDealResponse : PromptResponse
{
    public bool Cancelled { get; init; }
    public DealContents Contents { get; init; } = new();
}