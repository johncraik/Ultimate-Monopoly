using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Generic property selector. Asks <see cref="Prompt.PlayerId"/> to pick exactly
/// <see cref="Count"/> properties from <see cref="EligibleBoardIndexes"/>.
/// Reused for any property-pick context — mortgaging to cover a shortfall,
/// selling a building, handing a property into Free Parking, choosing which
/// of an opponent's properties to purge, etc. The framework does not
/// describe the intent; the call site uses <see cref="Prompt.Title"/> and
/// <see cref="Prompt.Body"/> to make the action clear to the player.
/// </summary>
/// <remarks>
/// As with <see cref="TargetPlayerPrompt"/>, the count is fixed by the
/// caller rather than offered as a range. For actions that benefit from a
/// per-step rule check — selling a building one at a time so each sale runs
/// through the even-building rule, for example — the engine loops with
/// <see cref="Count"/> set to 1 each iteration, re-deriving the eligible
/// set after every step.
/// </remarks>
public sealed class TargetPropertyPrompt : Prompt<TargetPropertyResponse>
{
    /// <summary>
    /// The candidate set of property <see cref="Snapshot.PropertyModel.BoardIndex"/>
    /// values the chooser must pick from. The engine populates this per
    /// context (e.g. for mortgaging, only the player's unmortgaged properties).
    /// </summary>
    public IReadOnlyList<ushort> EligibleBoardIndexes { get; init; } = [];

    private readonly ushort _count;

    /// <summary>
    /// How many properties must be selected. Fixed by the caller — not a range — but
    /// <b>clamped to the size of <see cref="EligibleBoardIndexes"/></b>: a caller can never
    /// require more selections than there are options, which would make the prompt
    /// unsatisfiable and lock the game. The clamped value is what both the client submit
    /// gate and <c>PromptValidator</c> enforce, so they stay in lockstep.
    /// </summary>
    public ushort Count
    {
        get => (ushort)Math.Min(_count, EligibleBoardIndexes.Count);
        init => _count = value;
    }

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}