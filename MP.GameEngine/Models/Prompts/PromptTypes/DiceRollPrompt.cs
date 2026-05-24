using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Input prompt for entering physical dice values. Covers both the standard
/// turn roll (3 dice) and card-forced rolls (1 or 2 dice). See
/// <c>design-docs/choice-events.md</c> §3 case 3 and §15.3.
/// </summary>
/// <remarks>
/// Setup dice (turn-order rolls before the game starts) are not handled
/// through this prompt — they go through the setup hub, outside the engine.
/// </remarks>
public sealed class DiceRollPrompt : Prompt<DiceRollResponse>
{
    /// <summary>
    /// How many dice the player must enter. Valid values are 1, 2, or 3. The
    /// response must populate exactly that many of <see cref="DiceRollResponse.Die1"/>,
    /// <see cref="DiceRollResponse.Die2"/>, <see cref="DiceRollResponse.ThirdDie"/>.
    /// </summary>
    public ushort DiceCount { get; init; }

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}
