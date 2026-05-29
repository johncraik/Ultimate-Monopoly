using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Opens when <see cref="Prompt.PlayerId"/> is leaving jail and must settle how:
/// pay the jail fee, or play a Get Out of Jail Free card. Choosing <i>whether</i>
/// to leave jail (pay / play card / attempt a double at turn start) is a command,
/// not a prompt — this prompt is only the pay-or-card settlement. See
/// <c>game-rules.md</c> Jail.
/// </summary>
public sealed class LeaveJailPrompt : Prompt<LeaveJailResponse>
{
    /// <summary>The jail fee the player would pay to leave (see <c>game-rules.md</c> Jail rule 3).</summary>
    public uint Cost { get; init; }

    /// <summary>
    /// Whether the player holds a Get Out of Jail Free card. The client only
    /// offers the <see cref="LeaveJailAction.PlayCard"/> option when this is
    /// <c>true</c>; the validator rejects a card response otherwise.
    /// </summary>
    public bool HasCard { get; init; }

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}