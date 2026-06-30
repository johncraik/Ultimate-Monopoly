namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;

/// <summary>The Audit spoke dashboard (C1) — JC.Core <c>AuditEntry</c> trends. Admin+. The audit table is
/// high-churn, so size/age are all-time counts and the breakdowns are scoped to a rolling 30-day window;
/// everything is a server-side aggregate (no rows pulled into memory).</summary>
public class AuditDashboardModel
{
    public required IReadOnlyList<MetricCard> Kpis { get; init; }
    public required SeriesWidget EntriesOverTime { get; init; }
    public required BreakdownWidget ByAction { get; init; }
    public required BreakdownWidget ActorSplit { get; init; }
    public required HistogramWidget DeleteActivity { get; init; }
    public required TopListWidget BusiestTables { get; init; }
    public required TopListWidget MostActiveActors { get; init; }
}

/// <summary>The hub's Audit tile — 3 KPIs (entries 24h · busiest table · system %) + entries-over-time.</summary>
public class AuditDashboardTile
{
    public required IReadOnlyList<MetricCard> Kpis { get; init; }
    public required SeriesWidget EntriesOverTime { get; init; }
}
