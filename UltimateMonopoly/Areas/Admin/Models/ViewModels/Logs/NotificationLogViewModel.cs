using JC.Communication.Logging.Models.Notifications;
using JC.Core.Extensions;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>One <see cref="NotificationLog"/> read/unread event: which notification, who performed it
/// (resolved to a username), whether it was a read or unread action, and when.</summary>
public class NotificationLogViewModel
{
    public string LogId { get; }
    public string NotificationId { get; }

    public string UserId { get; } = "-";
    public string Username { get; } = "Unknown";

    /// <summary>True for a read event, false for an unread event.</summary>
    public bool IsRead { get; }

    public string LogDate { get; }
    public string RelativeDate { get; }

    public NotificationLogViewModel(NotificationLog log, string? username = null)
    {
        LogId = log.Id;
        NotificationId = log.NotificationId;
        UserId = log.UserId;
        Username = username ?? "Unknown";
        IsRead = log.IsRead;
        LogDate = log.Timestamp.ToLocalTime().ToString("g");
        RelativeDate = log.Timestamp.ToRelativeTime();
    }
}
