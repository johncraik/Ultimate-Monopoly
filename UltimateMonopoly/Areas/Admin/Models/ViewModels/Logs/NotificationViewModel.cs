using JC.Communication.Notifications.Helpers;
using JC.Communication.Notifications.Models;
using JC.Core.Extensions;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>One <see cref="Notification"/> a user received — its content, type/styling, read + lifecycle
/// state, and its read/unread <see cref="NotificationLogViewModel"/> history (rendered in the row's accordion).</summary>
public class NotificationViewModel
{
    public string NotificationId { get; }
    public string RecipientUserId { get; }

    public string Title { get; }
    public string Body { get; }
    public string? BodyHtml { get; }

    public NotificationType Type { get; }
    public string TypeDisplay { get; }
    public string IconClass { get; }
    public string ColourClass { get; }

    public bool IsRead { get; }
    public string? ReadAt { get; }

    public string CreatedDate { get; }
    public string RelativeDate { get; }

    public string? ExpiresAt { get; }
    public bool IsExpired { get; }
    public bool IsDismissed { get; }

    public string? UrlLink { get; }

    public IReadOnlyList<NotificationLogViewModel> Logs { get; }

    public NotificationViewModel(Notification notification, IReadOnlyList<NotificationLogViewModel> logs)
    {
        NotificationId = notification.Id;
        RecipientUserId = notification.UserId;

        Title = notification.Title;
        Body = notification.Body;
        BodyHtml = notification.BodyHtml;

        Type = notification.Type;
        TypeDisplay = notification.Type.ToDisplayName();
        IconClass = NotificationUIHelper.GetIconClass(notification.Type);
        ColourClass = NotificationUIHelper.GetColourClass(notification.Type);

        IsRead = notification.IsRead;
        ReadAt = notification.ReadAtUtc?.ToLocalTime().ToString("g");

        CreatedDate = notification.CreatedUtc.ToLocalTime().ToString("g");
        RelativeDate = notification.CreatedUtc.ToRelativeTime();

        ExpiresAt = notification.ExpiresAtUtc?.ToLocalTime().ToString("g");
        IsExpired = notification.ExpiresAtUtc.HasValue && notification.ExpiresAtUtc.Value < DateTime.UtcNow;
        IsDismissed = notification.IsDeleted;

        UrlLink = notification.UrlLink;

        Logs = logs;
    }
}
