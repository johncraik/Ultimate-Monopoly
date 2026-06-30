namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;

/// <summary>The full Users spoke dashboard (C1 — /Admin/Users/Dashboard). Composed of the reusable widgets,
/// grouped into the page's sections. Built by <c>UserDashboardService.Build</c>.</summary>
public class UserDashboardModel
{
    // Triage row.
    public required IReadOnlyList<AlertWidget> Alerts { get; init; }

    // KPI strip.
    public required IReadOnlyList<MetricCard> Kpis { get; init; }

    // Account health.
    public required GaugeWidget EmailConfirmed { get; init; }
    public required GaugeWidget TwoFactor { get; init; }
    public required BreakdownWidget EnabledVsDisabled { get; init; }
    public required BreakdownWidget Roles { get; init; }

    // Activity.
    public required HistogramWidget LoginRecency { get; init; }
    public required IReadOnlyList<MetricCard> Dormancy { get; init; }

    // Gameplay population (not a leaderboard — population health).
    public required IReadOnlyList<MetricCard> WinLossDraw { get; init; }
    public required HistogramWidget WinRateDistribution { get; init; }

    // Customisation.
    public required GaugeWidget AvatarColour { get; init; }
    public required GaugeWidget AvatarImage { get; init; }
    public required TopListWidget TopAvatars { get; init; }

    // Trends (phase 2). Registrations is historical (from RegisteredUtc); Logins accrues from the daily
    // snapshot; Cohort retention is point-in-time (cohort month → % still active).
    public required SeriesWidget Registrations { get; init; }
    public required SeriesWidget Logins { get; init; }
    public required HistogramWidget CohortRetention { get; init; }
}

/// <summary>The hub's Users tile — the headline KPIs + the same alert widgets, built by
/// <c>UserDashboardService.BuildTile</c> from the same metric helpers the full spoke uses.</summary>
public class UserDashboardTile
{
    public required IReadOnlyList<MetricCard> Kpis { get; init; }
    public required IReadOnlyList<AlertWidget> Alerts { get; init; }
    public required SeriesWidget Registrations { get; init; }
}
