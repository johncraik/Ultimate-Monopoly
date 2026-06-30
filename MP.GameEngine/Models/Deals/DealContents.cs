namespace MP.GameEngine.Models.Deals;

/// <summary>
/// The full contents of a proposed deal — what each side puts up. Symmetric:
/// either side may offer cash and/or properties (a one-sided deal, e.g. a pure
/// gift or a pure purchase, is valid). Built by the proposer (on the Deal tab on
/// the turn-boundary path, or via a
/// <see cref="Prompts.PromptTypes.BuildDealPrompt"/> on the shortfall path) and
/// carried, server-authored, on the <see cref="Prompts.PromptTypes.DealPrompt"/>
/// to the counter party. Properties are referenced by board index; money is two
/// amounts (one per side) that the engine nets into a single signed move when it
/// applies the deal. See <c>design-docs/game-deals.md</c> §4.
/// </summary>
public sealed record DealContents
{
    /// <summary>Cash the proposer gives the counter party.</summary>
    public uint MoneyFromProposer { get; init; }

    /// <summary>Cash the counter party gives the proposer.</summary>
    public uint MoneyFromCounterParty { get; init; }

    /// <summary>Board indexes of the properties the proposer gives the counter party.</summary>
    public IReadOnlyList<ushort> PropertiesFromProposer { get; init; } = [];

    /// <summary>Board indexes of the properties the counter party gives the proposer.</summary>
    public IReadOnlyList<ushort> PropertiesFromCounterParty { get; init; } = [];

    /// <summary>
    /// True when neither side offers anything — no money and no property either way. Such a deal is a
    /// no-op ("nothing for nothing") and is rejected before it reaches the counter party (issue #17).
    /// </summary>
    public bool IsEmpty =>
        MoneyFromProposer == 0 && MoneyFromCounterParty == 0
        && PropertiesFromProposer.Count == 0 && PropertiesFromCounterParty.Count == 0;
}