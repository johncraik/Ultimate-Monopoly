using MP.GameEngine.Models.Deals;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Asks <see cref="Prompt.PlayerId"/> (the counter party) to accept or decline a
/// proposed deal. The single engine-initiated prompt on <b>both</b> deal paths —
/// the turn-boundary command and the shortfall settlement — since the proposer
/// always builds the deal and submits it whole, leaving only the accept/decline
/// to the engine. The <see cref="Contents"/> are server-authored (the counter
/// party never re-supplies them), so the response is a bare
/// <see cref="DealResponse.Accept"/> bool. There are no counter-offers: a decline
/// ends the deal (<c>game-deals.md</c> §9). The frontend renders the "what you
/// give / what you receive" summary by flipping <see cref="Contents"/> to the
/// counter party's perspective. See <c>design-docs/game-deals.md</c> §13.
/// </summary>
public sealed class DealPrompt : Prompt<DealResponse>
{
    /// <summary>The player who proposed the deal — the other side of the exchange (for display).</summary>
    public string ProposerId { get; init; } = "";

    /// <summary>
    /// The full deal, expressed from the <b>proposer's</b> perspective:
    /// <see cref="DealContents.MoneyFromProposer"/> /
    /// <see cref="DealContents.PropertiesFromProposer"/> are what the proposer
    /// gives (and so what the counter party receives), and vice versa. The
    /// frontend flips this for the counter party's "give / receive" summary.
    /// </summary>
    public DealContents Contents { get; init; } = new();

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}