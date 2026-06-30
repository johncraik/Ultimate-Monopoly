using JC.Communication.Logging.Models.Messaging;
using JC.Communication.Notifications.Models;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Github.Models;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Areas.Admin.Services.Dashboard;

/// <summary>
/// Builds the App Logs spoke dashboard (C1) and its hub tile — admin-action log + reported issues + comms
/// (notifications live; email/messaging show a "pending A1/E1" status until those features ship). Admin+. Logs
/// are aggregated server-side; the over-time series use server-side day-grouping (84 days) then weekly buckets.
/// </summary>
public class AppLogsDashboardService
{
    private const int WindowDays = 30;

    private readonly AppDbContext _context;

    public AppLogsDashboardService(AppDbContext context, IUserInfo userInfo)
    {
        _context = context;
        if (!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    private static List<SeriesPoint> WeeklyFromDays(DateTime now, Dictionary<DateTime, int> dayMap)
    {
        DateTime StartOfWeek(DateTime dt) => dt.Date.AddDays(-(((int)dt.DayOfWeek + 6) % 7)); // Monday-start
        var thisWeek = StartOfWeek(now);
        var points = new List<SeriesPoint>();
        for (var i = 11; i >= 0; i--)
        {
            var ws = thisWeek.AddDays(-7 * i);
            var we = ws.AddDays(7);
            points.Add(new SeriesPoint(ws.ToString("dd MMM"), dayMap.Where(kv => kv.Key >= ws && kv.Key < we).Sum(kv => kv.Value)));
        }
        return points;
    }

    private async Task<Dictionary<string, string>> ResolveUsernames(IEnumerable<string> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<string, string>();
        var rows = await _context.Users.AsNoTracking().Where(u => idList.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName }).ToListAsync();
        return rows.ToDictionary(x => x.Id, x => x.UserName ?? "Unknown");
    }

    private async Task<SeriesWidget> AdminActionsSeries(DateTime now, string id, string height)
    {
        var rows = await _context.AdminActionLogs.AsNoTracking()
            .Where(l => l.CreatedUtc >= now.AddDays(-84))
            .GroupBy(l => new { l.CreatedUtc.Year, l.CreatedUtc.Month, l.CreatedUtc.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() }).ToListAsync();
        var dayMap = rows.ToDictionary(x => new DateTime(x.Year, x.Month, x.Day), x => x.Count);
        return new SeriesWidget
        {
            Id = id, Title = "Admin actions · last 12 weeks", Icon = "bi-shield-lock", Tone = "primary",
            SeriesType = "Column", Height = height, Points = WeeklyFromDays(now, dayMap)
        };
    }

    public async Task<AppLogsDashboardTile> BuildTile()
    {
        var now = DateTime.UtcNow;

        var adminActions24h = await _context.AdminActionLogs.CountAsync(l => l.CreatedUtc >= now.AddDays(-1));
        var openIssues = await _context.ReportedIssues.CountAsync(i => !i.Closed);
        var notif7d = await _context.Notifications.CountAsync(n => n.CreatedUtc >= now.AddDays(-7));
        var syncFailures = await _context.ReportedIssues.CountAsync(i => !i.ReportSent);

        return new AppLogsDashboardTile
        {
            AdminActionsOverTime = await AdminActionsSeries(now, "tile-logs-admin", "160px"),
            Kpis = new[]
            {
                new MetricCard { Label = "Admin actions", Value = adminActions24h.ToString("N0"), Icon = "bi-shield-lock", Tone = "primary", Sub = "last 24h" },
                new MetricCard { Label = "Open issues", Value = openIssues.ToString("N0"), Icon = "bi-bug", Tone = openIssues > 0 ? "warning" : "secondary" },
                new MetricCard { Label = "Notifications", Value = notif7d.ToString("N0"), Icon = "bi-bell", Tone = "info", Sub = "last 7 days" }
            },
            Alerts = new[]
            {
                new AlertWidget { Label = "GitHub sync failures", Count = syncFailures, Tone = "danger", Icon = "bi-exclamation-triangle", Href = "/Admin/Logs/Issues/Index" }
            }
        };
    }

    public async Task<AppLogsDashboardModel> Build()
    {
        var now = DateTime.UtcNow;
        var winStart = now.AddDays(-WindowDays);

        // ===== Admin actions =====
        var adminActions24h = await _context.AdminActionLogs.CountAsync(l => l.CreatedUtc >= now.AddDays(-1));
        var reportersContacted = await _context.AdminActionLogs.CountAsync(l => l.Action == AdminActionType.IssueReporterContacted);
        var byTarget = await _context.AdminActionLogs.AsNoTracking()
            .GroupBy(l => l.TargetType).Select(g => new { Target = g.Key, Count = g.Count() }).ToListAsync();
        var adminByUser = await _context.AdminActionLogs.AsNoTracking().Where(l => l.CreatedById != null)
            .GroupBy(l => l.CreatedById).Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(8).ToListAsync();
        var adminNames = await ResolveUsernames(adminByUser.Select(x => x.UserId!));

        // ===== Issues =====
        var openIssues = await _context.ReportedIssues.CountAsync(i => !i.Closed);
        var closedIssues = await _context.ReportedIssues.CountAsync(i => i.Closed);
        var bugs = await _context.ReportedIssues.CountAsync(i => i.Type == IssueType.Bug);
        var suggestions = await _context.ReportedIssues.CountAsync(i => i.Type == IssueType.Suggestion);
        var syncFailures = await _context.ReportedIssues.CountAsync(i => !i.ReportSent);
        var topReportersRaw = await _context.ReportedIssues.AsNoTracking().Where(i => i.UserId != null)
            .GroupBy(i => i.UserId).Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(8).ToListAsync();
        var reporterNames = await ResolveUsernames(topReportersRaw.Select(x => x.UserId!));

        // ===== Notifications (30-day window) =====
        var notif7d = await _context.Notifications.CountAsync(n => n.CreatedUtc >= now.AddDays(-7));
        var notifWindow = _context.Notifications.AsNoTracking().Where(n => n.CreatedUtc >= winStart);
        var notifByType = await notifWindow.GroupBy(n => n.Type).Select(g => new { Type = g.Key, Count = g.Count() }).ToListAsync();
        var notifTotal = await notifWindow.CountAsync();
        var notifRead = await notifWindow.CountAsync(n => n.IsRead);
        var readRate = notifTotal == 0 ? 0 : Math.Round(100.0 * notifRead / notifTotal, 1);
        var notifDayRows = await _context.Notifications.AsNoTracking().Where(n => n.CreatedUtc >= now.AddDays(-84))
            .GroupBy(n => new { n.CreatedUtc.Year, n.CreatedUtc.Month, n.CreatedUtc.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() }).ToListAsync();
        var notifDayMap = notifDayRows.ToDictionary(x => new DateTime(x.Year, x.Month, x.Day), x => x.Count);

        // ===== Email (pending A1) =====
        var emailSent = await _context.EmailSentLogs.CountAsync();
        var emailSucceeded = await _context.EmailSentLogs.CountAsync(e => e.Succeeded);
        var emailFailures = emailSent - emailSucceeded;
        var emailRate = emailSent == 0 ? 0 : (int)Math.Round(100.0 * emailSucceeded / emailSent, 0);

        // ===== Messaging (pending E1) =====
        var messages = await _context.ThreadActivityLogs.CountAsync(t => t.ActivityType == ThreadActivityType.Message);

        return new AppLogsDashboardModel
        {
            Alerts = new[]
            {
                new AlertWidget { Label = "GitHub sync failures", Count = syncFailures, Tone = "danger", Icon = "bi-exclamation-triangle", Href = "/Admin/Logs/Issues/Index" },
                new AlertWidget { Label = "email failures", Count = emailFailures, Tone = "warning", Icon = "bi-envelope-exclamation", Href = "/Admin/Logs/Email/Index" }
            },
            Kpis = new[]
            {
                new MetricCard { Label = "Admin actions", Value = adminActions24h.ToString("N0"), Icon = "bi-shield-lock", Tone = "primary", Sub = "last 24h" },
                new MetricCard { Label = "Notifications", Value = notif7d.ToString("N0"), Icon = "bi-bell", Tone = "info", Sub = "last 7 days" },
                new MetricCard { Label = "Open issues", Value = openIssues.ToString("N0"), Icon = "bi-bug", Tone = openIssues > 0 ? "warning" : "secondary" },
                new MetricCard { Label = "Sync failures", Value = syncFailures.ToString("N0"), Icon = "bi-exclamation-triangle", Tone = syncFailures > 0 ? "danger" : "secondary" },
                new MetricCard { Label = "Reporters contacted", Value = reportersContacted.ToString("N0"), Icon = "bi-envelope", Tone = "secondary" }
            },

            AdminByTarget = new BreakdownWidget
            {
                Id = "logs-admin-target", Title = "Admin actions by target", Icon = "bi-bullseye", Style = BreakdownStyle.Donut,
                Segments = byTarget.OrderByDescending(x => x.Count).Select(x => new BreakdownSegment(x.Target.ToDisplayName(), x.Count)).ToList(),
                EmptyText = "No admin actions yet."
            },
            AdminLeaderboard = new TopListWidget
            {
                Title = "Most-active admins", Icon = "bi-person-badge",
                Rows = adminByUser.Select(x => new TopListRow(adminNames.GetValueOrDefault(x.UserId!, "Unknown"), $"{x.Count:N0}",
                    x.Count == 1 ? "1 action" : $"{x.Count:N0} actions")).ToList(),
                EmptyText = "No admin actions yet."
            },
            AdminActionsOverTime = await AdminActionsSeries(now, "logs-admin", "240px"),

            IssuesStatus = new BreakdownWidget
            {
                Id = "logs-issues-status", Title = "Issues — open vs closed", Icon = "bi-card-checklist", Style = BreakdownStyle.Donut,
                Segments = new List<BreakdownSegment> { new("Open", openIssues, DashboardPalette.Hex("warning")), new("Closed", closedIssues, DashboardPalette.Hex("secondary")) },
                EmptyText = "No issues reported."
            },
            IssuesByType = new BreakdownWidget
            {
                Id = "logs-issues-type", Title = "Issues — bugs vs suggestions", Icon = "bi-bug", Style = BreakdownStyle.Donut,
                Segments = new List<BreakdownSegment> { new("Bug", bugs, DashboardPalette.Hex("danger")), new("Suggestion", suggestions, DashboardPalette.Hex("info")) },
                EmptyText = "No issues reported."
            },
            TopReporters = new TopListWidget
            {
                Title = "Top reporters", Icon = "bi-person-up",
                Rows = topReportersRaw.Select(x => new TopListRow(reporterNames.GetValueOrDefault(x.UserId!, "Unknown"), $"{x.Count:N0}",
                    x.Count == 1 ? "1 issue" : $"{x.Count:N0} issues", $"/Admin/Users/Details/{x.UserId}")).ToList(),
                EmptyText = "No local reporters yet."
            },

            NotificationsByType = new BreakdownWidget
            {
                Id = "logs-notif-type", Title = "Notifications by type (30d)", Icon = "bi-bell", Style = BreakdownStyle.Donut,
                Segments = notifByType.OrderByDescending(x => x.Count).Select(x => new BreakdownSegment(x.Type.ToDisplayName(), x.Count)).ToList(),
                EmptyText = "No notifications in the window."
            },
            NotificationReadRate = new GaugeWidget { Id = "logs-notif-read", Label = "Notifications read", Percent = readRate, Tone = "info", Caption = $"{notifRead:N0} of {notifTotal:N0} (30d)", Icon = "bi-envelope-open" },
            NotificationsOverTime = new SeriesWidget { Id = "logs-notif", Title = "Notifications · last 12 weeks", Icon = "bi-bell", Tone = "info", SeriesType = "Column", Height = "240px", Points = WeeklyFromDays(now, notifDayMap) },

            EmailStatus = emailSent == 0
                ? new MetricCard { Label = "Email", Value = "—", Icon = "bi-envelope", Tone = "secondary", Sub = "Pending A1 — email not sent yet" }
                : new MetricCard { Label = "Email delivered", Value = $"{emailRate}%", Icon = "bi-envelope", Tone = emailFailures > 0 ? "warning" : "success", Sub = $"{emailSent:N0} sent · {emailFailures:N0} failed" },
            MessagingStatus = messages == 0
                ? new MetricCard { Label = "Messaging", Value = "—", Icon = "bi-chat-dots", Tone = "secondary", Sub = "Pending E1 — messaging not built" }
                : new MetricCard { Label = "Messages sent", Value = messages.ToString("N0"), Icon = "bi-chat-dots", Tone = "info" }
        };
    }
}
