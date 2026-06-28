namespace UltimateMonopoly.Models.ViewModels.Social;

/// <summary>A single message in a conversation, from the current user's perspective.</summary>
public record MessageView(bool FromMe, string Text, DateTime SentAtUtc, string MessageId);

/// <summary>A row in the Messages left panel — one DM conversation with a friend.</summary>
public class ConversationListItem
{
    public required string ThreadId { get; init; }
    public required string FriendUserId { get; init; }
    public required UserProfileViewModel Friend { get; init; }
    public string? Preview { get; init; }
    public DateTime? LastActivityUtc { get; init; }
    public bool Unread { get; init; }
}

/// <summary>The open conversation rendered in the main panel (header friend + messages + send-ability).</summary>
public class ConversationView
{
    public required string ThreadId { get; init; }
    public required string FriendUserId { get; init; }
    public required UserProfileViewModel Friend { get; init; }
    public required IReadOnlyList<MessageView> Messages { get; init; }
    public bool CanSend { get; init; }

    /// <summary>When the friend last read my most-recent message (only set if my message is the thread's latest
    /// and they've read it) — drives the small "Read @time" receipt.</summary>
    public DateTime? LastReadAtUtc { get; init; }
}
