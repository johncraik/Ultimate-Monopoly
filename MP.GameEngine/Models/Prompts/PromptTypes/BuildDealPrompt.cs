using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Asks <see cref="Prompt.PlayerId"/> (the debtor) to construct a deal with a
/// fixed counter party — the creditor — during a shortfall. Emitted only on the
/// shortfall settlement path: a debt owed to another player may be discharged by
/// a deal the creditor accepts (<c>game-rules.md</c> Default rule 7;
/// <c>transactions.md</c> §6). On the turn-boundary path there is no
/// <see cref="BuildDealPrompt"/> — the player builds the deal on the Deal tab and
/// submits it as a command. Either way the constructed
/// <see cref="BuildDealResponse.Contents"/> flows into the shared
/// <c>DealService</c> apply path. See <c>design-docs/game-deals.md</c> §7, §13.
/// </summary>
/// <remarks>
/// The counter party is <b>fixed</b> to the creditor (<see cref="CounterPartyId"/>)
/// — the debtor does not choose who to deal with here. The eligible-property
/// sets and cash caps are supplied by the engine so the client and validator
/// agree on what may be put up; the validator confirms the response stays within
/// them.
/// </remarks>
public sealed class BuildDealPrompt : Prompt<BuildDealResponse>
{
    /// <summary>The creditor the deal must be made with — fixed, not chosen by the debtor.</summary>
    public string CounterPartyId { get; init; } = "";

    /// <summary>
    /// The proposer's (debtor's) available cash. Money they offer cannot exceed
    /// this — deal spending comes from cash on hand (<c>game-rules.md</c> Default
    /// rule 7); the validator enforces it.
    /// </summary>
    public uint ProposerBalance { get; init; }

    /// <summary>The counter party's (creditor's) available cash. Money they offer cannot exceed this.</summary>
    public uint CounterPartyBalance { get; init; }

    /// <summary>
    /// Board indexes of the proposer's dealable properties — owned, not reserved,
    /// not built-on (and not in a built-on set); mortgaged is allowed. The engine
    /// computes this set; the validator confirms every property the proposer puts
    /// up is drawn from it. See <c>game-deals.md</c> §5.
    /// </summary>
    public IReadOnlyList<ushort> ProposerDealableIndexes { get; init; } = [];

    /// <summary>Board indexes of the counter party's dealable properties (same eligibility rule).</summary>
    public IReadOnlyList<ushort> CounterPartyDealableIndexes { get; init; } = [];

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}