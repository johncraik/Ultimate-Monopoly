using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels.Social;

namespace UltimateMonopoly.Areas.Admin.Services.Dashboard;

/// <summary>
/// Builds the Community spoke dashboard (C1) — social graph (friends / requests / blocks) + moderation
/// (reports / blocked-words) — and the hub's Community tile, from the same metric helpers. All cheap live
/// queries (no precompute job): the social tables are low-volume, so each is pulled as a light projection and
/// aggregated in memory. Admin- / SystemAdmin-gated.
/// </summary>
public class CommunityDashboardService
{
    private readonly AppDbContext _context;

    public CommunityDashboardService(AppDbContext context, IUserInfo userInfo)
    {
        _context = context;
        if (!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    // ---- shared helpers ----

    private static List<SeriesPoint> WeeklyPoints(DateTime now, List<DateTime> timestamps)
    {
        DateTime StartOfWeek(DateTime dt) => dt.Date.AddDays(-(((int)dt.DayOfWeek + 6) % 7)); // Monday-start
        var thisWeek = StartOfWeek(now);
        var points = new List<SeriesPoint>();
        for (var i = 11; i >= 0; i--)
        {
            var ws = thisWeek.AddDays(-7 * i);
            var we = ws.AddDays(7);
            points.Add(new SeriesPoint(ws.ToString("dd MMM"), timestamps.Count(t => t >= ws && t < we)));
        }
        return points;
    }

    private static SeriesWidget WeeklySeries(DateTime now, List<DateTime> ts, string id, string title, string icon, string tone, string height) =>
        new() { Id = id, Title = title, Icon = icon, Tone = tone, SeriesType = "Column", Height = height, Points = WeeklyPoints(now, ts) };

    private static string Dur(double hours) => hours <= 0 ? "—" : hours < 48 ? $"{hours:0.#}h" : $"{hours / 24:0.#}d";

    /// <summary>The strongest action a (flags) resolution represents, for the "by outcome" bucketing.</summary>
    private static string HighestAction(ReportResolution res)
    {
        if (res == ReportResolution.Open) return "Open";
        if (res.HasFlag(ReportResolution.AccountDeleted)) return "Deleted";
        if (res.HasFlag(ReportResolution.AccountDisabled)) return "Disabled";
        if (res.HasFlag(ReportResolution.AccountRestricted)) return "Restricted";
        return "Handled";
    }

    private async Task<Dictionary<string, string>> ResolveUsernames(IEnumerable<string> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<string, string>();
        var rows = await _context.Users.AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName }).ToListAsync();
        return rows.ToDictionary(x => x.Id, x => x.UserName ?? "Unknown");
    }

    // ---- hub tile ----

    /// <summary>The hub's Community tile — open reports / active blocks / new friendships + the open-reports alert + reports-over-time.</summary>
    public async Task<CommunityDashboardTile> BuildTile()
    {
        var now = DateTime.UtcNow;

        var openReports = await _context.ReportedUsers.FilterDeleted(DeletedQueryType.OnlyActive)
            .CountAsync(r => r.Resolution == ReportResolution.Open);
        var activeBlocks = await _context.BlockedUsers.FilterDeleted(DeletedQueryType.OnlyActive).CountAsync();
        var newFriends7d = await _context.Friends.FilterDeleted(DeletedQueryType.OnlyActive)
            .CountAsync(f => f.DateRemovedUtc == null && f.CreatedUtc > now.AddDays(-7));
        var reportDates = await _context.ReportedUsers.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(r => r.CreatedUtc >= now.AddDays(-84)).Select(r => r.CreatedUtc).ToListAsync();

        return new CommunityDashboardTile
        {
            ReportsOverTime = WeeklySeries(now, reportDates, "tile-comm-reports", "Reports · last 12 weeks", "bi-flag", "warning", "160px"),
            Kpis = new[]
            {
                new MetricCard { Label = "Open reports", Value = openReports.ToString("N0"), Icon = "bi-flag", Tone = openReports > 0 ? "warning" : "secondary" },
                new MetricCard { Label = "Active blocks", Value = activeBlocks.ToString("N0"), Icon = "bi-slash-circle", Tone = "secondary" },
                new MetricCard { Label = "New friends", Value = newFriends7d.ToString("N0"), Icon = "bi-people", Tone = "success", Sub = "last 7 days" }
            },
            Alerts = new[]
            {
                new AlertWidget { Label = "open reports", Count = openReports, Tone = "warning", Icon = "bi-flag", Href = "/Admin/Reports/Index?Resolution=Open" }
            }
        };
    }

    // ---- full spoke ----

    public async Task<CommunityDashboardModel> Build()
    {
        var now = DateTime.UtcNow;
        var totalUsers = await _context.Users.CountAsync();

        // ---- Reports ----
        var reports = await _context.ReportedUsers.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .Select(r => new { r.Reason, r.Resolution, r.CreatedUtc, r.LastModifiedUtc, r.BlockedId })
            .ToListAsync();

        var totalReports = reports.Count;
        var openReports = reports.Count(r => r.Resolution == ReportResolution.Open);
        var handled = totalReports - openReports;
        var resolutionRate = totalReports == 0 ? 0 : Math.Round(100.0 * handled / totalReports, 1);

        var resolveHours = reports.Where(r => r.Resolution != ReportResolution.Open && r.LastModifiedUtc.HasValue)
            .Select(r => (r.LastModifiedUtc!.Value - r.CreatedUtc).TotalHours).Where(h => h >= 0).ToList();
        var avgResolveHours = resolveHours.Count > 0 ? resolveHours.Average() : 0;

        var oldestOpen = reports.Where(r => r.Resolution == ReportResolution.Open)
            .Select(r => (DateTime?)r.CreatedUtc).Min();
        var oldestOpenDays = oldestOpen.HasValue ? (int)(now - oldestOpen.Value).TotalDays : 0;
        var priority = reports.Count(r => r.Resolution == ReportResolution.Open
            && (r.Reason == ReportReason.SelfHarm || r.Reason == ReportReason.Threats));

        var reasonSegments = reports.GroupBy(r => r.Reason)
            .Select(g => new BreakdownSegment(g.Key.ToDisplayName(), g.Count()))
            .OrderByDescending(s => s.Value).ToList();

        var stateOrder = new[] { "Open", "Handled", "Restricted", "Disabled", "Deleted" };
        var stateCounts = reports.GroupBy(r => HighestAction(r.Resolution)).ToDictionary(g => g.Key, g => g.Count());
        var stateBuckets = stateOrder.Select(s => new HistogramBucket(s, stateCounts.GetValueOrDefault(s))).ToList();

        // Most-reported users: report → its BlockedUser → the reported person.
        var blockedIds = reports.Select(r => r.BlockedId).Distinct().ToList();
        var blockedMap = await _context.BlockedUsers.AsNoTracking()
            .Where(b => blockedIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BlockedUserId }).ToListAsync();
        var idToReported = blockedMap.ToDictionary(x => x.Id, x => x.BlockedUserId);
        var reportedCounts = reports.Where(r => idToReported.ContainsKey(r.BlockedId))
            .GroupBy(r => idToReported[r.BlockedId])
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(5).ToList();
        var reportedNames = await ResolveUsernames(reportedCounts.Select(x => x.UserId));
        var mostReportedRows = reportedCounts.Select(x => new TopListRow(
            reportedNames.GetValueOrDefault(x.UserId, "Unknown"), x.Count.ToString("N0"),
            x.Count == 1 ? "1 report" : $"{x.Count} reports", $"/Admin/Users/Details/{x.UserId}")).ToList();

        // ---- Friend requests ----
        var requests = await _context.FriendRequests.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .Select(r => new { r.IsAccepted, r.CreatedUtc, r.AcknowledgedAtUtc }).ToListAsync();
        var pending = requests.Count(r => r.IsAccepted == null);
        var actedOn = requests.Count(r => r.IsAccepted != null);
        var accepted = requests.Count(r => r.IsAccepted == true);
        var acceptanceRate = actedOn == 0 ? 0 : Math.Round(100.0 * accepted / actedOn, 1);
        var responseHours = requests.Where(r => r.AcknowledgedAtUtc.HasValue)
            .Select(r => (r.AcknowledgedAtUtc!.Value - r.CreatedUtc).TotalHours).Where(h => h >= 0).ToList();
        var avgResponseHours = responseHours.Count > 0 ? responseHours.Average() : 0;

        // ---- Friendships (all rows — formed/removed trend; active = not removed and not block-deleted) ----
        var friends = await _context.Friends.AsNoTracking()
            .Select(f => new { f.CreatedUtc, f.DateRemovedUtc, f.IsDeleted, f.DeletedUtc }).ToListAsync();
        var activeFriendships = friends.Count(f => !f.IsDeleted && f.DateRemovedUtc == null);
        var newFriends7d = friends.Count(f => !f.IsDeleted && f.DateRemovedUtc == null && f.CreatedUtc > now.AddDays(-7));
        var formedPoints = WeeklyPoints(now, friends.Select(f => f.CreatedUtc).ToList());
        var removedTimestamps = friends
            .Select(f => f.DateRemovedUtc ?? (f.IsDeleted ? f.DeletedUtc : null))
            .Where(t => t.HasValue).Select(t => t!.Value).ToList();
        var removedPoints = WeeklyPoints(now, removedTimestamps);

        // ---- Blocks ----
        var blocks = await _context.BlockedUsers.AsNoTracking().FilterDeleted(DeletedQueryType.OnlyActive)
            .Select(b => new { b.CreatedById, b.BlockedUserId, b.CreatedUtc }).ToListAsync();
        var activeBlocks = blocks.Count;
        var blockerCount = blocks.Where(b => b.CreatedById != null).Select(b => b.CreatedById).Distinct().Count();
        var blockPrevalence = totalUsers == 0 ? 0 : Math.Round(100.0 * blockerCount / totalUsers, 1);
        var blockedCounts = blocks.GroupBy(b => b.BlockedUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(5).ToList();
        var blockedNames = await ResolveUsernames(blockedCounts.Select(x => x.UserId));
        var mostBlockedRows = blockedCounts.Select(x => new TopListRow(
            blockedNames.GetValueOrDefault(x.UserId, "Unknown"), x.Count.ToString("N0"),
            x.Count == 1 ? "1 block" : $"{x.Count} blocks", $"/Admin/Users/Details/{x.UserId}")).ToList();

        // ---- Blocked words ----
        var blockedWords = await _context.BlockedWords.AsNoTracking().Select(w => w.CreatedUtc).ToListAsync();
        var wordCount = blockedWords.Count;
        var wordsAdded30 = blockedWords.Count(c => c > now.AddDays(-30));

        return new CommunityDashboardModel
        {
            Alerts = new[]
            {
                new AlertWidget { Label = "open reports", Count = openReports, Tone = "warning", Icon = "bi-flag", Href = "/Admin/Reports/Index?Resolution=Open" },
                new AlertWidget { Label = "priority (self-harm / threats)", Count = priority, Tone = "danger", Icon = "bi-exclamation-octagon", Href = "/Admin/Reports/Index?Resolution=Open" },
                new AlertWidget { Label = "days · oldest unresolved", Count = openReports > 0 ? oldestOpenDays : 0, Tone = "warning", Icon = "bi-hourglass-split" }
            },
            Kpis = new[]
            {
                new MetricCard { Label = "Open reports", Value = openReports.ToString("N0"), Icon = "bi-flag", Tone = openReports > 0 ? "warning" : "secondary" },
                new MetricCard { Label = "Active friendships", Value = activeFriendships.ToString("N0"), Icon = "bi-people", Tone = "success", Sub = newFriends7d > 0 ? $"+{newFriends7d:N0} this week" : null },
                new MetricCard { Label = "Pending requests", Value = pending.ToString("N0"), Icon = "bi-person-plus", Tone = "info" },
                new MetricCard { Label = "Active blocks", Value = activeBlocks.ToString("N0"), Icon = "bi-slash-circle", Tone = "secondary" },
                new MetricCard { Label = "Blocked words", Value = wordCount.ToString("N0"), Icon = "bi-shield-x", Tone = "secondary", Sub = wordsAdded30 > 0 ? $"+{wordsAdded30:N0} (30d)" : null }
            },

            ResolutionRate = new GaugeWidget { Id = "comm-resolution", Label = "Reports resolved", Percent = resolutionRate, Tone = "success", Caption = $"{handled:N0} of {totalReports:N0}", Icon = "bi-check2-circle" },
            AvgTimeToResolve = new MetricCard { Label = "Avg time to resolve", Value = Dur(avgResolveHours), Icon = "bi-hourglass", Tone = "info" },
            ReportsByReason = new BreakdownWidget { Id = "comm-reasons", Title = "Reports by reason", Icon = "bi-flag", Style = BreakdownStyle.Donut, Segments = reasonSegments, EmptyText = "No reports yet." },
            ReportsByState = new HistogramWidget { Id = "comm-states", Title = "Reports by outcome", Icon = "bi-shield-check", Tone = "warning", Buckets = stateBuckets },
            MostReportedUsers = new TopListWidget { Title = "Most-reported users", Icon = "bi-person-exclamation", Rows = mostReportedRows, EmptyText = "No reports yet." },

            AcceptanceRate = new GaugeWidget { Id = "comm-acceptance", Label = "Requests accepted", Percent = acceptanceRate, Tone = "info", Caption = $"{accepted:N0} of {actedOn:N0}", Icon = "bi-person-check" },
            AvgResponseTime = new MetricCard { Label = "Avg request response", Value = Dur(avgResponseHours), Icon = "bi-reply", Tone = "info" },
            BlockPrevalence = new GaugeWidget { Id = "comm-blockprev", Label = "Users who've blocked", Percent = blockPrevalence, Tone = "secondary", Caption = $"{blockerCount:N0} of {totalUsers:N0}", Icon = "bi-slash-circle" },
            MostBlockedUsers = new TopListWidget { Title = "Most-blocked users", Icon = "bi-person-dash", Rows = mostBlockedRows, EmptyText = "No blocks yet." },

            ReportsOverTime = WeeklySeries(now, reports.Select(r => r.CreatedUtc).ToList(), "comm-reports", "Reports · last 12 weeks", "bi-flag", "warning", "240px"),
            BlocksOverTime = WeeklySeries(now, blocks.Select(b => b.CreatedUtc).ToList(), "comm-blocks", "Blocks · last 12 weeks", "bi-slash-circle", "danger", "240px"),
            Friendships = new MultiSeriesWidget
            {
                Id = "comm-friendships", Title = "Friendships · formed vs removed (12 weeks)", Icon = "bi-people",
                Series = new[]
                {
                    new NamedSeries("Formed", "success", "Column", formedPoints),
                    new NamedSeries("Removed", "danger", "Column", removedPoints)
                }
            }
        };
    }
}
