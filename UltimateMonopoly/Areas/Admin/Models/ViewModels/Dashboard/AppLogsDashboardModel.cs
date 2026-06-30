namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;

/// <summary>The App Logs spoke dashboard (C1) — the admin-action log + reported issues + comms (notifications
/// now; email/messaging surface once A1/E1 land). Admin+ (issues also visible to GithubManager elsewhere).</summary>
public class AppLogsDashboardModel
{
    public required IReadOnlyList<AlertWidget> Alerts { get; init; }
    public required IReadOnlyList<MetricCard> Kpis { get; init; }

    // Admin actions
    public required BreakdownWidget AdminByTarget { get; init; }
    public required TopListWidget AdminLeaderboard { get; init; }
    public required SeriesWidget AdminActionsOverTime { get; init; }

    // Issues
    public required BreakdownWidget IssuesStatus { get; init; }
    public required BreakdownWidget IssuesByType { get; init; }
    public required TopListWidget TopReporters { get; init; }

    // Communications
    public required BreakdownWidget NotificationsByType { get; init; }
    public required GaugeWidget NotificationReadRate { get; init; }
    public required SeriesWidget NotificationsOverTime { get; init; }
    public required MetricCard EmailStatus { get; init; }
    public required MetricCard MessagingStatus { get; init; }
}

/// <summary>The hub's App Logs tile — 3 KPIs (admin actions 24h · open issues · notifications 7d) + admin-actions-over-time.</summary>
public class AppLogsDashboardTile
{
    public required IReadOnlyList<MetricCard> Kpis { get; init; }
    public required IReadOnlyList<AlertWidget> Alerts { get; init; }
    public required SeriesWidget AdminActionsOverTime { get; init; }
}
