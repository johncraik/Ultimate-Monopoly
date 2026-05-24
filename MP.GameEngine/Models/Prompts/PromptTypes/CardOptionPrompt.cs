using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Generic n-ary selector for card handlers that present a labelled choice
/// ("pay £200 OR draw a Chance card", "advance to nearest station OR pay
/// £50"). Composes with <see cref="TargetPlayerPrompt"/> and
/// <see cref="TargetPropertyPrompt"/> for chained card flows — pick an
/// option here, then pick a target in a follow-on prompt.
/// </summary>
/// <remarks>
/// The framework deliberately does not model the per-card effects — each
/// card has its own handler (<c>game-engine.md</c> §11). The prompt only
/// carries the choice; the handler constructs the option list and acts on
/// the selection. Stable string keys (not list indexes) identify options so
/// that the response is meaningful in logs and survives any reordering in
/// future card revisions.
/// </remarks>
public sealed class CardOptionPrompt : Prompt<CardOptionResponse>
{
    //TODO: This may change when card framework is implemented
    
    /// <summary>
    /// The set of options the player must choose from. Keys must be
    /// distinct; labels are display text. The engine is responsible for
    /// building this list — n-ary by convention (at least two options); a
    /// single-option list would be a non-choice and should not open this
    /// prompt at all.
    /// </summary>
    public IReadOnlyList<CardOption> Options { get; init; } = [];

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}

/// <summary>
/// A single labelled option in a <see cref="CardOptionPrompt"/>.
/// <see cref="Key"/> is the stable identifier returned in the response;
/// <see cref="Label"/> is the player-facing display text.
/// </summary>
public sealed record CardOption(string Key, string Label);