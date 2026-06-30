namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;

/// <summary>A time-series (Syncfusion line / spline-area chart) — e.g. registrations or logins over time.</summary>
public class SeriesWidget
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Icon { get; init; }
    public string Tone { get; init; } = "primary";
    public string SeriesType { get; init; } = "SplineArea";   // EJ2 series type (Line / Spline / SplineArea / Column)
    public required IReadOnlyList<SeriesPoint> Points { get; init; }
    public string Height { get; init; } = "240px";            // shorter on a hub tile, taller on a spoke page
    public string EmptyText { get; init; } = "No data yet.";
}

public record SeriesPoint(string Label, double Value);

/// <summary>A multi-series chart (Syncfusion) — several named series over the same category axis, e.g.
/// friendships formed vs removed. Each <see cref="NamedSeries"/> renders as its own coloured series + legend entry.</summary>
public class MultiSeriesWidget
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Icon { get; init; }
    public string Height { get; init; } = "240px";
    public required IReadOnlyList<NamedSeries> Series { get; init; }
    public string EmptyText { get; init; } = "No data yet.";
}

public record NamedSeries(string Name, string Tone, string SeriesType, IReadOnlyList<SeriesPoint> Points);

/// <summary>A hub tile for one spoke — its headline KPIs + alerts + a "View →" link. <see cref="Href"/> null =
/// the spoke isn't built yet (renders a "coming soon" placeholder), so the hub layout is complete from day one.</summary>
public class SpokeTileModel
{
    public required string Title { get; init; }
    public required string Icon { get; init; }
    public string Tone { get; init; } = "primary";
    public string? Href { get; init; }
    public IReadOnlyList<MetricCard>? Kpis { get; init; }
    public IReadOnlyList<AlertWidget>? Alerts { get; init; }
    /// <summary>The tile's trend graph(s) — one per spoke (Games has two). Rendered bare (no card) under the KPIs.</summary>
    public IReadOnlyList<SeriesWidget>? Graphs { get; init; }
    public bool ComingSoon => string.IsNullOrEmpty(Href);
}