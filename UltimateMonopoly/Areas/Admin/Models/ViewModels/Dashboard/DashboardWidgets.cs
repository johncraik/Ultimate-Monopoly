namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;

// Reusable, presentation-shaped widget models. The dashboard hub and every spoke page compose from these and
// render them through the shared partials in Pages/Shared/Dashboard — one source of data shape + presentation.

/// <summary>A single KPI card — a preformatted headline number with optional delta / sub-caption / link.</summary>
public class MetricCard
{
    public required string Label { get; init; }
    public required string Value { get; init; }   // already formatted (e.g. "1,284", "82%")
    public string? Icon { get; init; }            // Bootstrap icon class, e.g. "bi-people"
    public string Tone { get; init; } = "primary"; // Bootstrap contextual tone
    public string? Sub { get; init; }             // small caption under the value
    public string? Href { get; init; }            // optional link (e.g. a pre-filtered list page)
    public MetricDelta? Delta { get; init; }      // optional trend delta vs the prior window
}

/// <summary>A trend delta on a <see cref="MetricCard"/> — the formatted change, its direction, and whether up is good.</summary>
public record MetricDelta(string Value, bool Up, bool GoodWhenUp = true)
{
    public string Tone => Up == GoodWhenUp ? "success" : "danger";
    public string Icon => Up ? "bi-arrow-up-right" : "bi-arrow-down-right";
}

/// <summary>A triage alert badge — rendered only when <see cref="Count"/> &gt; 0. Drives the hub alert strip
/// and any spoke "needs attention" row.</summary>
public class AlertWidget
{
    public required string Label { get; init; }
    public required int Count { get; init; }
    public string Tone { get; init; } = "warning";
    public string? Icon { get; init; }
    public string? Href { get; init; }
    public bool Show => Count > 0;
}

/// <summary>A 0–100% ring "gauge" (rendered as a Syncfusion donut with a centred percentage).</summary>
public class GaugeWidget
{
    public required string Id { get; init; }      // unique DOM id (two charts on a page must not collide)
    public required string Label { get; init; }
    public required double Percent { get; init; } // 0–100
    public string Tone { get; init; } = "primary";
    public string? Caption { get; init; }         // e.g. "1,050 of 1,284"
    public string? Icon { get; init; }
}

public enum BreakdownStyle { Donut, Pie }

/// <summary>A categorical breakdown (Syncfusion accumulation chart — donut or pie).</summary>
public class BreakdownWidget
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Icon { get; init; }
    public BreakdownStyle Style { get; init; } = BreakdownStyle.Donut;
    public required IReadOnlyList<BreakdownSegment> Segments { get; init; }
    public string? EmptyText { get; init; } = "No data.";
}

/// <summary>One slice of a <see cref="BreakdownWidget"/>. <paramref name="Colour"/> null = auto from the palette.</summary>
public record BreakdownSegment(string Label, double Value, string? Colour = null);

/// <summary>A distribution (Syncfusion column chart) — fixed buckets with counts.</summary>
public class HistogramWidget
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Icon { get; init; }
    public string Tone { get; init; } = "primary";
    public required IReadOnlyList<HistogramBucket> Buckets { get; init; }
}

public record HistogramBucket(string Label, double Count);

/// <summary>A small ranked list (top-N) rendered as a compact table.</summary>
public class TopListWidget
{
    public required string Title { get; init; }
    public string? Icon { get; init; }
    public required IReadOnlyList<TopListRow> Rows { get; init; }
    public string EmptyText { get; init; } = "Nothing to show.";
}

public record TopListRow(string Label, string Value, string? Secondary = null, string? Href = null);

/// <summary>Tone → hex + the shared series palette, so the chart partials colour consistently on the dark theme.</summary>
public static class DashboardPalette
{
    public static string Hex(string tone) => tone switch
    {
        "primary" => "#0d6efd",
        "success" => "#198754",
        "danger" => "#dc3545",
        "warning" => "#ffc107",
        "info" => "#0dcaf0",
        "secondary" => "#6c757d",
        _ => "#0d6efd"
    };

    /// <summary>The "remainder" arc of a gauge ring (subtle on the dark theme).</summary>
    public const string Remainder = "#343a40";

    /// <summary>Distinct series colours for auto-coloured breakdown slices.</summary>
    public static readonly string[] Series =
        { "#0d6efd", "#198754", "#dc3545", "#ffc107", "#0dcaf0", "#6c757d", "#6610f2", "#fd7e14" };
}
