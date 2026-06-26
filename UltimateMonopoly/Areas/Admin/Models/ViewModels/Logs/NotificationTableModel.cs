using JC.Communication.Notifications.Models;
using JC.Core.Models.Pagination;
using UltimateMonopoly.Areas.Admin.Enums;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>Backs the <c>_NotificationsTable</c> partial (the per-user notifications list) plus the current
/// search / type / read-state / status filters, so the pagination links and no-JS fallback carry them.</summary>
public class NotificationTableModel
{
    public PagedList<NotificationViewModel> Notifications { get; }
    public string RecipientUserId { get; }

    public string? Search { get; }
    public NotificationType? Type { get; }
    public bool? Read { get; }
    public NotificationStatusFilter Status { get; }

    /// <summary>Preview mode (the §7.3 Recent Activity panel): the partial drops its pagination + count header.</summary>
    public bool Preview { get; init; }

    public NotificationTableModel(PagedList<NotificationViewModel> notifications, string recipientUserId,
        string? search, NotificationType? type, bool? read, NotificationStatusFilter status)
    {
        Notifications = notifications;
        RecipientUserId = recipientUserId;
        Search = search;
        Type = type;
        Read = read;
        Status = status;
    }
}
