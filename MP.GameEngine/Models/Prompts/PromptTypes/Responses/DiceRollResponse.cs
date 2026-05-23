namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of a <see cref="PromptTypes.DiceRollPrompt"/>. The named-field
/// shape (rather than a flat list) forces the consumer to explicitly state
/// which die is the third die — no implicit "last element of a 3-element list"
/// convention.
/// </summary>
/// <remarks>
/// Which fields are populated depends on
/// <see cref="PromptTypes.DiceRollPrompt.DiceCount"/>:
/// <list type="bullet">
/// <item><c>DiceCount == 1</c> — only <see cref="Die1"/> is set.</item>
/// <item><c>DiceCount == 2</c> — <see cref="Die1"/> and <see cref="Die2"/> are set; <see cref="ThirdDie"/> is null.</item>
/// <item><c>DiceCount == 3</c> — all three are set.</item>
/// </list>
/// The validator rejects responses that do not match the expected population.
/// All values must be in the range 1–6.
/// </remarks>
public sealed class DiceRollResponse : PromptResponse
{
    /// <summary>Always required. The first die value (1–6).</summary>
    public ushort Die1 { get; init; }

    /// <summary>Required when <c>DiceCount &gt;= 2</c>; otherwise must be null.</summary>
    public ushort? Die2 { get; init; }

    /// <summary>Required when <c>DiceCount == 3</c>; otherwise must be null. Explicitly named so the consumer cannot confuse it with a regular die.</summary>
    public ushort? ThirdDie { get; init; }
}
