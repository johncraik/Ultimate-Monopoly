namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;

/// <summary>The Games spoke dashboard (C1 — lifecycle, players/length, board/config, storage, activity).
/// SystemAdmin-only. The deep economy/gameplay analytics from PlayerGameStat are a separate backlog.</summary>
public class GamesDashboardModel
{
    public required IReadOnlyList<AlertWidget> Alerts { get; init; }
    public required IReadOnlyList<MetricCard> Kpis { get; init; }

    // Lifecycle
    public required BreakdownWidget GamesByState { get; init; }
    public required GaugeWidget CompletionRate { get; init; }
    public required GaugeWidget CancellationRate { get; init; }
    public required BreakdownWidget OutcomeSplit { get; init; }

    // Players & length
    public required HistogramWidget PlayersPerGame { get; init; }
    public required MetricCard AvgPlayers { get; init; }
    public required HistogramWidget GameLength { get; init; }
    public required MetricCard AvgTurns { get; init; }
    public required TopListWidget LongestGames { get; init; }

    // Board & config
    public required HistogramWidget RoundingRules { get; init; }
    public required BreakdownWidget BoardUsage { get; init; }
    public required TopListWidget TopBoards { get; init; }

    // Storage
    public required MetricCard TotalStorage { get; init; }
    public required TopListWidget TopGamesBySize { get; init; }

    // Trends
    public required SeriesWidget GamesCreated { get; init; }
    public required SeriesWidget GamesConcluded { get; init; }
    public required SeriesWidget TurnThroughput { get; init; }
}

/// <summary>The hub's Games tile (full width) — 3 KPIs + the abandonment alert + two graphs (created &amp; concluded).</summary>
public class GamesDashboardTile
{
    public required IReadOnlyList<MetricCard> Kpis { get; init; }
    public required IReadOnlyList<AlertWidget> Alerts { get; init; }
    public required SeriesWidget GamesCreated { get; init; }
    public required SeriesWidget GamesConcluded { get; init; }
}
