namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of a <see cref="PromptTypes.LeaveJailPrompt"/> — how the player
/// settles leaving jail.
/// </summary>
public sealed class LeaveJailResponse : PromptResponse
{
    public LeaveJailAction Action { get; init; }
}

/// <summary>How a player settles leaving jail.</summary>
public enum LeaveJailAction
{
    /// <summary>Pay the jail fee (see <c>game-rules.md</c> Jail rule 3).</summary>
    PayFee,

    /// <summary>Play a Get Out of Jail Free card. Only valid when the player holds one.</summary>
    PlayCard
}