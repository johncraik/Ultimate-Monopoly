using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels;

namespace UltimateMonopoly.Areas.Admin.Services.Dashboard;

/// <summary>
/// Builds the Users spoke dashboard (C1 — /Admin/Users/Dashboard) and the hub's Users tile from the same
/// metric helpers, so a metric is shaped once and reused on both surfaces. All v1 metrics are cheap live
/// queries (no precompute job): the full build is a single projection of the lightweight user columns +
/// one grouped role-count query, aggregated in memory. Admin- / SystemAdmin-gated.
/// </summary>
public class UserDashboardService
{
    private readonly AppDbContext _context;

    public UserDashboardService(AppDbContext context, IUserInfo userInfo)
    {
        _context = context;
        if (!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    private record UserRow(bool IsEnabled, bool EmailConfirmed, bool TwoFactorEnabled, DateTimeOffset? LockoutEnd,
        int AccessFailedCount, DateTime? LastLoginUtc, DateTime? LastActiveUtc, uint Wins, uint Losses, uint Draws,
        string? AvatarColour, string? AvatarImageName, DateTime? RegisteredUtc);

    /// <summary>Counts of users per role (Admin / SystemAdmin / Restricted / HiddenUser / GithubManager), one grouped query.</summary>
    private async Task<Dictionary<string, int>> RoleCounts()
    {
        var rows = await (from ur in _context.UserRoles
                          join r in _context.Roles on ur.RoleId equals r.Id
                          group ur by r.Name into g
                          select new { Role = g.Key, Count = g.Count() }).ToListAsync();
        return rows.Where(x => x.Role != null).ToDictionary(x => x.Role!, x => x.Count);
    }

    /// <summary>Registrations per week over the last 12 weeks. Shared by the spoke page (tall) and the hub tile
    /// (short) so the graph is computed and shaped once.</summary>
    private static SeriesWidget RegistrationsSeries(DateTime now, List<DateTime?> registeredUtcs, string id, string height)
    {
        DateTime StartOfWeek(DateTime dt) => dt.Date.AddDays(-(((int)dt.DayOfWeek + 6) % 7)); // Monday-start
        var thisWeek = StartOfWeek(now);
        var points = new List<SeriesPoint>();
        for (var i = 11; i >= 0; i--)
        {
            var ws = thisWeek.AddDays(-7 * i);
            var we = ws.AddDays(7);
            points.Add(new SeriesPoint(ws.ToString("dd MMM"),
                registeredUtcs.Count(r => r.HasValue && r >= ws && r < we)));
        }
        return new SeriesWidget
        {
            Id = id, Title = "Registrations · last 12 weeks", Icon = "bi-person-plus", Tone = "primary",
            SeriesType = "SplineArea", Points = points, Height = height, EmptyText = "No registrations recorded yet."
        };
    }

    /// <summary>The hub's Users tile — three headline KPIs + the lockout alert + the registrations graph (cheap, runs on every landing).</summary>
    public async Task<UserDashboardTile> BuildTile()
    {
        var now = DateTime.UtcNow;
        var nowOffset = DateTimeOffset.UtcNow;
        var activeCutoff = now.AddMinutes(-5);

        var total = await _context.Users.CountAsync();
        var activeNow = await _context.Users.CountAsync(u => u.LastActiveUtc != null && u.LastActiveUtc > activeCutoff);
        var disabled = await _context.Users.CountAsync(u => !u.IsEnabled);
        var lockedOut = await _context.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > nowOffset);
        var restricted = (await RoleCounts()).GetValueOrDefault(AppRoles.Restricted);

        var regUtcs = await _context.Users
            .Where(u => u.RegisteredUtc != null && u.RegisteredUtc >= now.AddDays(-84))
            .Select(u => u.RegisteredUtc).ToListAsync();

        return new UserDashboardTile
        {
            Registrations = RegistrationsSeries(now, regUtcs, "tile-users-reg", "160px"),
            Kpis = new[]
            {
                new MetricCard { Label = "Users", Value = total.ToString("N0"), Icon = "bi-people", Tone = "primary", Href = "/Admin/Users/Index" },
                new MetricCard { Label = "Active now", Value = activeNow.ToString("N0"), Icon = "bi-broadcast", Tone = "success" },
                new MetricCard { Label = "Disabled", Value = disabled.ToString("N0"), Icon = "bi-person-x",
                    Tone = disabled > 0 ? "warning" : "secondary", Sub = restricted > 0 ? $"{restricted:N0} restricted" : null }
            },
            Alerts = new[]
            {
                new AlertWidget { Label = "locked out", Count = lockedOut, Tone = "danger", Icon = "bi-lock", Href = "/Admin/Users/Index" }
            }
        };
    }

    /// <summary>The full Users spoke dashboard.</summary>
    public async Task<UserDashboardModel> Build()
    {
        var now = DateTime.UtcNow;
        var nowOffset = DateTimeOffset.UtcNow;

        var users = await _context.Users.AsNoTracking().Select(u => new UserRow(
            u.IsEnabled, u.EmailConfirmed, u.TwoFactorEnabled, u.LockoutEnd, u.AccessFailedCount,
            u.LastLoginUtc, u.LastActiveUtc, u.NumberOfWins, u.NumberOfLosses, u.NumberOfDraws,
            u.AvatarColour, u.AvatarImageName, u.RegisteredUtc)).ToListAsync();
        var roles = await RoleCounts();

        var total = users.Count;
        double Pct(int n) => total == 0 ? 0 : Math.Round(100.0 * n / total, 1);

        var enabled = users.Count(u => u.IsEnabled);
        var disabled = total - enabled;
        var confirmed = users.Count(u => u.EmailConfirmed);
        var twoFa = users.Count(u => u.TwoFactorEnabled);
        var lockedOut = users.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd > nowOffset);
        var failedWatch = users.Count(u => u.AccessFailedCount > 0);
        var activeNow = users.Count(u => u.LastActiveUtc.HasValue && u.LastActiveUtc > now.AddMinutes(-5));
        var withColour = users.Count(u => !string.IsNullOrEmpty(u.AvatarColour));
        var withImage = users.Count(u => !string.IsNullOrEmpty(u.AvatarImageName));

        var restricted = roles.GetValueOrDefault(AppRoles.Restricted);
        var dormant30 = users.Count(u => !u.LastActiveUtc.HasValue || u.LastActiveUtc < now.AddDays(-30));
        var dormant60 = users.Count(u => !u.LastActiveUtc.HasValue || u.LastActiveUtc < now.AddDays(-60));
        var dormant90 = users.Count(u => !u.LastActiveUtc.HasValue || u.LastActiveUtc < now.AddDays(-90));

        var wins = users.Sum(u => (long)u.Wins);
        var losses = users.Sum(u => (long)u.Losses);
        var draws = users.Sum(u => (long)u.Draws);

        // Login-recency buckets (point-in-time, from the latest LastLoginUtc).
        var loginBuckets = new List<HistogramBucket>
        {
            new("< 1 day", users.Count(u => u.LastLoginUtc.HasValue && u.LastLoginUtc.Value > now.AddDays(-1))),
            new("1–7 days", users.Count(u => u.LastLoginUtc.HasValue && u.LastLoginUtc.Value <= now.AddDays(-1) && u.LastLoginUtc.Value > now.AddDays(-7))),
            new("7–30 days", users.Count(u => u.LastLoginUtc.HasValue && u.LastLoginUtc.Value <= now.AddDays(-7) && u.LastLoginUtc.Value > now.AddDays(-30))),
            new("30–90 days", users.Count(u => u.LastLoginUtc.HasValue && u.LastLoginUtc.Value <= now.AddDays(-30) && u.LastLoginUtc.Value > now.AddDays(-90))),
            new("90 days+", users.Count(u => u.LastLoginUtc.HasValue && u.LastLoginUtc.Value <= now.AddDays(-90))),
            new("Never", users.Count(u => !u.LastLoginUtc.HasValue))
        };

        // Win-rate distribution across players who have finished at least one game.
        var rates = users.Where(u => (u.Wins + u.Losses + u.Draws) > 0)
            .Select(u => 100.0 * u.Wins / (u.Wins + u.Losses + u.Draws)).ToList();
        HistogramBucket Wr(string label, double lo, double hi) =>
            new(label, rates.Count(r => r >= lo && (hi >= 100 ? r <= hi : r < hi)));
        var winRateBuckets = new List<HistogramBucket>
        {
            Wr("0–20%", 0, 20), Wr("20–40%", 20, 40), Wr("40–60%", 40, 60), Wr("60–80%", 60, 80), Wr("80–100%", 80, 100)
        };

        // Most-used avatar images.
        var topAvatars = users.Where(u => !string.IsNullOrEmpty(u.AvatarImageName))
            .GroupBy(u => u.AvatarImageName!)
            .Select(g => new TopListRow(g.Key, $"{g.Count():N0}", g.Count() == 1 ? "1 user" : $"{g.Count()} users"))
            .OrderByDescending(r => r.Value).Take(5).ToList();

        // ---- Trends (phase 2) ----
        var registrations = RegistrationsSeries(now, users.Select(u => u.RegisteredUtc).ToList(), "trend-reg", "240px");

        var loginRows = await _context.DailyActivityStats.AsNoTracking()
            .Where(d => d.Date >= now.Date.AddDays(-30))
            .OrderBy(d => d.Date).ToListAsync();
        var loginPoints = loginRows.Select(d => new SeriesPoint(d.Date.ToString("dd MMM"), d.Logins)).ToList();

        var cohortBuckets = users.Where(u => u.RegisteredUtc.HasValue)
            .GroupBy(u => new DateTime(u.RegisteredUtc!.Value.Year, u.RegisteredUtc.Value.Month, 1))
            .OrderByDescending(g => g.Key).Take(6).OrderBy(g => g.Key)
            .Select(g => new HistogramBucket(g.Key.ToString("MMM yy"),
                Math.Round(100.0 * g.Count(u => u.LastActiveUtc.HasValue && u.LastActiveUtc > now.AddDays(-30)) / g.Count(), 1)))
            .ToList();

        var success = DashboardPalette.Hex("success");
        var danger = DashboardPalette.Hex("danger");

        return new UserDashboardModel
        {
            Alerts = new[]
            {
                new AlertWidget { Label = "locked out", Count = lockedOut, Tone = "danger", Icon = "bi-lock", Href = "/Admin/Users/Index" },
                new AlertWidget { Label = "with failed sign-ins", Count = failedWatch, Tone = "warning", Icon = "bi-exclamation-triangle" }
            },

            Kpis = new[]
            {
                new MetricCard { Label = "Total users", Value = total.ToString("N0"), Icon = "bi-people", Tone = "primary" },
                new MetricCard { Label = "Active now", Value = activeNow.ToString("N0"), Icon = "bi-broadcast", Tone = "success", Sub = "last 5 min" },
                new MetricCard { Label = "Enabled", Value = enabled.ToString("N0"), Icon = "bi-person-check", Tone = "info", Sub = $"{disabled:N0} disabled" },
                new MetricCard { Label = "Restricted", Value = restricted.ToString("N0"), Icon = "bi-shield-exclamation", Tone = restricted > 0 ? "warning" : "secondary" },
                new MetricCard { Label = "Dormant 30d", Value = dormant30.ToString("N0"), Icon = "bi-moon", Tone = "secondary" }
            },

            EmailConfirmed = new GaugeWidget { Id = "gauge-email", Label = "Email confirmed", Percent = Pct(confirmed), Tone = "success", Caption = $"{confirmed:N0} of {total:N0}", Icon = "bi-envelope-check" },
            TwoFactor = new GaugeWidget { Id = "gauge-2fa", Label = "2FA enabled", Percent = Pct(twoFa), Tone = "info", Caption = $"{twoFa:N0} of {total:N0}", Icon = "bi-shield-lock" },
            EnabledVsDisabled = new BreakdownWidget
            {
                Id = "bd-enabled", Title = "Enabled vs disabled", Icon = "bi-toggles", Style = BreakdownStyle.Donut,
                Segments = new List<BreakdownSegment> { new("Enabled", enabled, success), new("Disabled", disabled, danger) }
            },
            Roles = new BreakdownWidget
            {
                Id = "bd-roles", Title = "Roles", Icon = "bi-person-badge", Style = BreakdownStyle.Donut,
                Segments = new List<BreakdownSegment>
                {
                    new("Admin", roles.GetValueOrDefault(SystemRoles.Admin)),
                    new("System Admin", roles.GetValueOrDefault(SystemRoles.SystemAdmin)),
                    new("GitHub Manager", roles.GetValueOrDefault(AppRoles.GithubManager)),
                    new("Restricted", roles.GetValueOrDefault(AppRoles.Restricted)),
                    new("Hidden", roles.GetValueOrDefault(AppRoles.HiddenUser))
                }
            },

            LoginRecency = new HistogramWidget { Id = "hist-login", Title = "Last login", Icon = "bi-clock-history", Tone = "info", Buckets = loginBuckets },
            Dormancy = new[]
            {
                new MetricCard { Label = "Dormant 30d", Value = dormant30.ToString("N0"), Icon = "bi-moon", Tone = "secondary" },
                new MetricCard { Label = "Dormant 60d", Value = dormant60.ToString("N0"), Icon = "bi-moon-stars", Tone = "secondary" },
                new MetricCard { Label = "Dormant 90d", Value = dormant90.ToString("N0"), Icon = "bi-moon-fill", Tone = "secondary" }
            },

            WinLossDraw = new[]
            {
                new MetricCard { Label = "Wins", Value = wins.ToString("N0"), Icon = "bi-trophy", Tone = "success" },
                new MetricCard { Label = "Losses", Value = losses.ToString("N0"), Icon = "bi-x-circle", Tone = "danger" },
                new MetricCard { Label = "Draws", Value = draws.ToString("N0"), Icon = "bi-dash-circle", Tone = "secondary" }
            },
            WinRateDistribution = new HistogramWidget { Id = "hist-winrate", Title = "Win-rate distribution", Icon = "bi-bar-chart", Tone = "success", Buckets = winRateBuckets },

            AvatarColour = new GaugeWidget { Id = "gauge-avatar-colour", Label = "Custom colour", Percent = Pct(withColour), Tone = "primary", Caption = $"{withColour:N0} of {total:N0}", Icon = "bi-palette" },
            AvatarImage = new GaugeWidget { Id = "gauge-avatar-image", Label = "Custom image", Percent = Pct(withImage), Tone = "primary", Caption = $"{withImage:N0} of {total:N0}", Icon = "bi-image" },
            TopAvatars = new TopListWidget { Title = "Most-used avatar images", Icon = "bi-images", Rows = topAvatars, EmptyText = "No custom avatar images yet." },

            Registrations = registrations,
            Logins = new SeriesWidget { Id = "trend-logins", Title = "Logins · last 30 days", Icon = "bi-box-arrow-in-right", Tone = "info", SeriesType = "Column", Points = loginPoints, EmptyText = "No data yet — accrues nightly." },
            CohortRetention = new HistogramWidget { Id = "hist-cohort", Title = "Cohort retention · % active in last 30d", Icon = "bi-graph-up-arrow", Tone = "success", Buckets = cohortBuckets }
        };
    }
}
