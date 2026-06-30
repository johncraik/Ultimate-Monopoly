using JC.Core.Models;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels;

namespace UltimateMonopoly.Services.Statistics;

/// <summary>
/// Captures one <see cref="DailyActivityStat"/> per day (the previous, completed UTC day) so the admin
/// dashboard can trend logins / DAU / WAU / MAU — history the live tables can't reconstruct, since
/// <c>LastLoginUtc</c>/<c>LastActiveUtc</c> store only the latest value. Idempotent (upserts the day's row);
/// recurring Hangfire job, just after midnight. Approximate by construction (latest-activity only), which is
/// the standard trade-off for activity trends without a per-event log.
/// </summary>
public class DailyStatsJob : IBackgroundJob
{
    private readonly AppDbContext _context;
    private readonly ILogger<DailyStatsJob> _logger;

    public DailyStatsJob(AppDbContext context, ILogger<DailyStatsJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var day = today.AddDays(-1);   // the completed day this run records
        var dayEnd = today;

        var total = await _context.Users.CountAsync(cancellationToken);
        var newUsers = await _context.Users.CountAsync(u => u.RegisteredUtc >= day && u.RegisteredUtc < dayEnd, cancellationToken);
        var logins = await _context.Users.CountAsync(u => u.LastLoginUtc >= day && u.LastLoginUtc < dayEnd, cancellationToken);
        var dau = await _context.Users.CountAsync(u => u.LastActiveUtc >= day && u.LastActiveUtc < dayEnd, cancellationToken);
        var wau = await _context.Users.CountAsync(u => u.LastActiveUtc >= day.AddDays(-6) && u.LastActiveUtc < dayEnd, cancellationToken);
        var mau = await _context.Users.CountAsync(u => u.LastActiveUtc >= day.AddDays(-29) && u.LastActiveUtc < dayEnd, cancellationToken);

        var row = await _context.DailyActivityStats.FirstOrDefaultAsync(d => d.Date == day, cancellationToken);
        if (row == null)
        {
            row = new DailyActivityStat { Date = day };
            _context.DailyActivityStats.Add(row);
        }

        row.TotalUsers = total;
        row.NewUsers = newUsers;
        row.Logins = logins;
        row.Dau = dau;
        row.Wau = wau;
        row.Mau = mau;

        await _context.SaveChangesAsync(cancellationToken);

        // Trim history beyond the dashboard's 30-day window — the logins/DAU graph only reads the last 30 days,
        // so older snapshots are dead weight.
        var trimmed = await _context.DailyActivityStats
            .Where(d => d.Date < today.AddDays(-30))
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Daily activity snapshot for {Day:yyyy-MM-dd}: {Total} users, {New} new, {Logins} logins, {Dau} DAU ({Trimmed} old row(s) trimmed)",
            day, total, newUsers, logins, dau, trimmed);
    }
}