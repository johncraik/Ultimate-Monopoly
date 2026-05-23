namespace MP.GameEngine.Models.Prompts;

/// <summary>
/// Describes the audience of a prompt — who will see it. Authorisation
/// (who may submit which response variant) is enforced separately by
/// <see cref="Services.Framework.PromptValidator"/>.
/// </summary>
public sealed record PromptTarget(
    PromptTargetKind Kind,
    IReadOnlyList<string> PlayerIds)
{
    public static PromptTarget SinglePlayer(string playerId) =>
        new(PromptTargetKind.Single, [playerId]);

    public static PromptTarget Group(IEnumerable<string> playerIds) =>
        new(PromptTargetKind.Group, playerIds.ToArray());
}

public enum PromptTargetKind
{
    /// <summary>
    /// Sent to a single player for a response.
    /// Multiple responses from players sequentially are single responses in a loop
    /// </summary>
    Single,
    
    /// <summary>
    /// Sent to multiple players where the first response wins
    /// </summary>
    Group
}
