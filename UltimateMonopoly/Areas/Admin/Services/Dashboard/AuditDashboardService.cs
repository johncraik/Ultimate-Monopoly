using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Areas.Admin.Services.Dashboard;

/// <summary>
/// Builds the Audit spoke dashboard (C1) and the hub's Audit tile from the JC.Core <c>AuditEntry</c> trail.
/// Admin+. The trail is the highest-churn table in the app, so <b>nothing is pulled into memory</b>: size/age
/// are all-time <c>COUNT</c>/<c>MIN</c>, and the breakdowns are server-side <c>GROUP BY</c> over a rolling
/// 30-day window. <c>UserName</c> is stored on each entry, so actor lists need no user lookup.
/// </summary>
public class AuditDashboardService
{
    private const int WindowDays = 30;

    private readonly AppDbContext _context;

    public AuditDashboardService(AppDbContext context, IUserInfo userInfo)
    {
        _context = context;
        if (!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    /// <summary>12 weekly buckets summing per-day counts (the entries-over-time series, server-side day-grouped).</summary>
    private static List<SeriesPoint> WeeklyFromDays(DateTime now, Dictionary<DateTime, int> dayMap)
    {
        DateTime StartOfWeek(DateTime dt) => dt.Date.AddDays(-(((int)dt.DayOfWeek + 6) % 7)); // Monday-start
        var thisWeek = StartOfWeek(now);
        var points = new List<SeriesPoint>();
        for (var i = 11; i >= 0; i--)
        {
            var ws = thisWeek.AddDays(-7 * i);
            var we = ws.AddDays(7);
            var c = dayMap.Where(kv => kv.Key >= ws && kv.Key < we).Sum(kv => kv.Value);
            points.Add(new SeriesPoint(ws.ToString("dd MMM"), c));
        }
        return points;
    }

    private async Task<SeriesWidget> EntriesSeries(DateTime now, string id, string height)
    {
        var dayCounts = await _context.AuditEntries.AsNoTracking()
            .Where(a => a.AuditDate >= now.AddDays(-84))
            .GroupBy(a => new { a.AuditDate.Year, a.AuditDate.Month, a.AuditDate.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .ToListAsync();
        var dayMap = dayCounts.ToDictionary(x => new DateTime(x.Year, x.Month, x.Day), x => x.Count);
        return new SeriesWidget
        {
            Id = id, Title = "Audit entries · last 12 weeks", Icon = "bi-clipboard-data", Tone = "warning",
            SeriesType = "Column", Height = height, Points = WeeklyFromDays(now, dayMap)
        };
    }

    public async Task<AuditDashboardTile> BuildTile()
    {
        var now = DateTime.UtcNow;

        var entries24h = await _context.AuditEntries.CountAsync(a => a.AuditDate >= now.AddDays(-1));

        var busiest = await _context.AuditEntries.AsNoTracking()
            .Where(a => a.AuditDate >= now.AddDays(-7))
            .GroupBy(a => a.TableName).Select(g => new { Table = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).FirstOrDefaultAsync();

        var total7 = await _context.AuditEntries.CountAsync(a => a.AuditDate >= now.AddDays(-7));
        var system7 = await _context.AuditEntries.CountAsync(a => a.AuditDate >= now.AddDays(-7) && a.UserId == IUserInfo.SYSTEM_USER_ID);
        var systemPct = total7 == 0 ? 0 : Math.Round(100.0 * system7 / total7, 0);

        return new AuditDashboardTile
        {
            EntriesOverTime = await EntriesSeries(now, "tile-audit-entries", "160px"),
            Kpis = new[]
            {
                new MetricCard { Label = "Entries (24h)", Value = entries24h.ToString("N0"), Icon = "bi-clipboard-data", Tone = "warning" },
                new MetricCard { Label = "Busiest table", Value = busiest?.Table ?? "—", Icon = "bi-table", Tone = "secondary", Sub = busiest != null ? $"{busiest.Count:N0} (7d)" : null },
                new MetricCard { Label = "System actor", Value = $"{systemPct:0}%", Icon = "bi-robot", Tone = "info", Sub = "last 7 days" }
            }
        };
    }

    public async Task<AuditDashboardModel> Build()
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddDays(-WindowDays);
        var win = _context.AuditEntries.AsNoTracking().Where(a => a.AuditDate >= windowStart);

        // Size / age (all-time).
        var total = await _context.AuditEntries.CountAsync();
        var oldest = await _context.AuditEntries.MinAsync(a => (DateTime?)a.AuditDate);
        var entries24h = await _context.AuditEntries.CountAsync(a => a.AuditDate >= now.AddDays(-1));
        var entries7d = await _context.AuditEntries.CountAsync(a => a.AuditDate >= now.AddDays(-7));

        // Window aggregates.
        var winTotal = await win.CountAsync();
        var distinctActors = await win.Select(a => a.UserId).Distinct().CountAsync();

        var byAction = await win.GroupBy(a => a.Action).Select(g => new { Action = g.Key, Count = g.Count() }).ToListAsync();
        var softDeletes = byAction.Where(x => x.Action == AuditAction.SoftDelete).Sum(x => x.Count);
        var restores = byAction.Where(x => x.Action == AuditAction.Restore).Sum(x => x.Count);

        var byTable = await win.GroupBy(a => a.TableName).Select(g => new { Table = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(8).ToListAsync();

        var byActor = await win.GroupBy(a => new { a.UserId, a.UserName }).Select(g => new { g.Key.UserId, g.Key.UserName, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(8).ToListAsync();

        var systemCount = await win.CountAsync(a => a.UserId == IUserInfo.SYSTEM_USER_ID);
        var unknownCount = await win.CountAsync(a => a.UserId == IUserInfo.UNKNOWN_USER_ID);
        var humanCount = winTotal - systemCount - unknownCount;

        var oldestDays = oldest.HasValue ? (int)(now - oldest.Value).TotalDays : 0;

        return new AuditDashboardModel
        {
            Kpis = new[]
            {
                new MetricCard { Label = "Total entries", Value = total.ToString("N0"), Icon = "bi-clipboard-data", Tone = "primary" },
                new MetricCard { Label = "Entries (24h)", Value = entries24h.ToString("N0"), Icon = "bi-clock-history", Tone = "warning" },
                new MetricCard { Label = "Entries (7d)", Value = entries7d.ToString("N0"), Icon = "bi-calendar-week", Tone = "info" },
                new MetricCard { Label = "Distinct actors", Value = distinctActors.ToString("N0"), Icon = "bi-people", Tone = "secondary", Sub = "last 30 days" },
                new MetricCard { Label = "Oldest entry", Value = oldest.HasValue ? $"{oldestDays:N0}d" : "—", Icon = "bi-hourglass-bottom", Tone = "secondary", Sub = "retention window" }
            },

            EntriesOverTime = await EntriesSeries(now, "audit-entries", "240px"),

            ByAction = new BreakdownWidget
            {
                Id = "audit-actions", Title = "By action (30d)", Icon = "bi-pencil-square", Style = BreakdownStyle.Donut,
                Segments = byAction.OrderByDescending(x => x.Count)
                    .Select(x => new BreakdownSegment(x.Action.ToDisplayName(), x.Count)).ToList(),
                EmptyText = "No audit activity in the window."
            },
            ActorSplit = new BreakdownWidget
            {
                Id = "audit-actors", Title = "Actor (30d)", Icon = "bi-person-gear", Style = BreakdownStyle.Donut,
                Segments = new List<BreakdownSegment>
                {
                    new("Human", humanCount, DashboardPalette.Hex("primary")),
                    new("System", systemCount, DashboardPalette.Hex("info")),
                    new("Unknown", unknownCount, DashboardPalette.Hex("secondary"))
                },
                EmptyText = "No audit activity in the window."
            },
            DeleteActivity = new HistogramWidget
            {
                Id = "audit-deletes", Title = "Soft-delete vs restore (30d)", Icon = "bi-arrow-counterclockwise", Tone = "warning",
                Buckets = new List<HistogramBucket> { new("Soft-deletes", softDeletes), new("Restores", restores) }
            },

            BusiestTables = new TopListWidget
            {
                Title = "Busiest tables (30d)", Icon = "bi-table",
                Rows = byTable.Select(x => new TopListRow(x.Table ?? "(unknown)", $"{x.Count:N0}",
                    x.Count == 1 ? "1 entry" : $"{x.Count:N0} entries", $"/Admin/Audit/Data/Table/{x.Table}")).ToList(),
                EmptyText = "No audit activity in the window."
            },
            MostActiveActors = new TopListWidget
            {
                Title = "Most-active actors (30d)", Icon = "bi-person-lines-fill",
                Rows = byActor.Select(x => new TopListRow(
                    string.IsNullOrEmpty(x.UserName) ? (x.UserId ?? "Unknown") : x.UserName, $"{x.Count:N0}",
                    x.Count == 1 ? "1 action" : $"{x.Count:N0} actions")).ToList(),
                EmptyText = "No audit activity in the window."
            }
        };
    }
}
