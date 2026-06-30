namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;

/// <summary>The Community spoke dashboard (C1) — social graph (friends / requests / blocks) + moderation
/// (reports / restrictions / blocked-words). Accessed only via the hub tile (no nav entry). Admin+.</summary>
public class CommunityDashboardModel
{
    public required IReadOnlyList<AlertWidget> Alerts { get; init; }
    public required IReadOnlyList<MetricCard> Kpis { get; init; }

    // Moderation
    public required GaugeWidget ResolutionRate { get; init; }
    public required MetricCard AvgTimeToResolve { get; init; }
    public required BreakdownWidget ReportsByReason { get; init; }
    public required HistogramWidget ReportsByState { get; init; }
    public required TopListWidget MostReportedUsers { get; init; }

    // Social graph
    public required GaugeWidget AcceptanceRate { get; init; }
    public required MetricCard AvgResponseTime { get; init; }
    public required GaugeWidget BlockPrevalence { get; init; }
    public required TopListWidget MostBlockedUsers { get; init; }

    // Trends
    public required SeriesWidget ReportsOverTime { get; init; }
    public required SeriesWidget BlocksOverTime { get; init; }
    public required MultiSeriesWidget Friendships { get; init; }
}

/// <summary>The hub's Community tile — 3 headline KPIs + the open-reports alert + the reports-over-time graph.</summary>
public class CommunityDashboardTile
{
    public required IReadOnlyList<MetricCard> Kpis { get; init; }
    public required IReadOnlyList<AlertWidget> Alerts { get; init; }
    public required SeriesWidget ReportsOverTime { get; init; }
}
