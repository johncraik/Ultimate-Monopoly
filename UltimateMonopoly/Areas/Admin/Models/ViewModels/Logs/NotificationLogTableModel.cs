using JC.Core.Models.Pagination;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>Backs the dual-mode <c>_NotificationLogsTable</c> partial.
/// <para><b>Global</b> (the read/unread Logs page) — paginated, shows the Notification column.</para>
/// <para><b>Scoped</b> (inside a notification's accordion on the user page) — a plain list, no pagination,
/// hides the Notification column (it's fixed). Mirrors the <c>_AuditTable</c> <c>IsUserTrail</c> convention.</para></summary>
public class NotificationLogTableModel
{
    public IReadOnlyList<NotificationLogViewModel> Items { get; }

    /// <summary>The paged source — non-null only in the global Logs-page mode (drives the pagination control).</summary>
    public PagedList<NotificationLogViewModel>? Paged { get; }

    /// <summary>Scoped to one notification (accordion) → hide the Notification column.</summary>
    public bool ScopedToNotification { get; }

    public string? Search { get; }
    public bool? Read { get; }

    /// <summary>Global Logs-page mode — paginated + filter controls.</summary>
    public NotificationLogTableModel(PagedList<NotificationLogViewModel> paged, string? search, bool? read)
    {
        Paged = paged;
        Items = paged.ToList();
        Search = search;
        Read = read;
        ScopedToNotification = false;
    }

    /// <summary>Scoped accordion mode — one notification's logs, no pagination.</summary>
    public NotificationLogTableModel(IReadOnlyList<NotificationLogViewModel> items)
    {
        Items = items;
        ScopedToNotification = true;
    }
}
