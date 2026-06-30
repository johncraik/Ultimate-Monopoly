using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Generic player selector. Asks <see cref="Prompt.PlayerId"/> to pick exactly
/// <see cref="Count"/> players from <see cref="EligiblePlayerIds"/>. Reused
/// by any engine path that needs a player target — card effects, deal
/// initiation, etc. The framework deliberately does not describe the intent;
/// the call site sets <see cref="Prompt.Title"/> and <see cref="Prompt.Body"/>
/// to communicate why the choice is being made.
/// </summary>
/// <remarks>
/// <see cref="Count"/> is fixed by the caller, not offered as a range — the
/// player never decides how many to pick. Where two cards in the same deck
/// differ in how many targets they affect (e.g. one card targets one
/// player, another targets two), they remain separate cards with their own
/// fixed counts; each card's handler opens this prompt with its own
/// concrete <see cref="Count"/>.
/// </remarks>
public sealed class TargetPlayerPrompt : Prompt<TargetPlayerResponse>
{
    /// <summary>The candidate set the chooser must pick from. The engine populates this per context.</summary>
    public IReadOnlyList<string> EligiblePlayerIds { get; init; } = [];

    private readonly ushort _count;

    /// <summary>
    /// How many players must be selected. Fixed by the caller — not a range — but
    /// <b>clamped to the size of <see cref="EligiblePlayerIds"/></b>: a caller can never
    /// require more selections than there are options, which would make the prompt
    /// unsatisfiable and lock the game. The clamped value is what both the client submit
    /// gate and <c>PromptValidator</c> enforce, so they stay in lockstep.
    /// </summary>
    public ushort Count
    {
        get => (ushort)Math.Min(_count, EligiblePlayerIds.Count);
        init => _count = value;
    }

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}