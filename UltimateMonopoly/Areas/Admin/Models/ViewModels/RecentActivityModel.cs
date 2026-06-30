using UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels;

/// <summary>
/// The composed view model behind the reusable Recent Activity panel (C1 §7.3) — a user-keyed dashboard of
/// activity streams, each a sub-<c>TableModel</c> rendered in preview mode by its own existing partial.
/// A stream is <b>null when hidden</b> (not applicable to this viewer/user): <see cref="RecentGames"/> only
/// when the viewer is a SystemAdmin; <see cref="AdminLogs"/> only when the user holds an admin role or has
/// ≥1 admin-log entry. The panel owns no queries — <c>RecentActivityService.Build</c> composes them; the
/// panel just renders each non-null stream plus its "View all →" link (pre-scoped to the user).
/// </summary>
public class RecentActivityModel
{
    public string UserId { get; }

    // ---- Streams (null = hidden) ----
    public ThreadActivityLogTableModel? MessageLogs { get; init; }
    public GameTableModel? RecentGames { get; init; }
    public AdminLogTableModel? AdminLogs { get; init; }
    public EmailLogTableModel? EmailLogs { get; init; }
    public NotificationTableModel? Notifications { get; init; }
    public AuditTableModel? UserTrail { get; init; }

    // ---- "View all →" targets, pre-scoped to the user (C1 §7.4 decision 1) ----
    public string MessageLogsUrl { get; }
    public string RecentGamesUrl { get; }
    public string AdminLogsUrl { get; }
    public string EmailLogsUrl { get; }
    public string NotificationsUrl { get; }
    public string UserTrailUrl { get; }

    public RecentActivityModel(string userId)
    {
        UserId = userId;
        var enc = Uri.EscapeDataString(userId);
        MessageLogsUrl = $"/Admin/Logs/Messaging/Index?Search={enc}";
        RecentGamesUrl = $"/Admin/Games/Index?Search={enc}";
        AdminLogsUrl = $"/Admin/Logs/Admin/Index?Search={enc}";
        EmailLogsUrl = $"/Admin/Logs/Email/Index?Search={enc}";
        NotificationsUrl = $"/Admin/Logs/Notifications/User/{enc}";
        UserTrailUrl = $"/Admin/Audit/Users/Trail/{enc}";
    }
}