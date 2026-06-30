using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Retention cleanup for the <see cref="AdminActionLog"/> accountability trail (C1 — design-docs/c1-admin-area.md
/// §5 / §14). Permanently (hard) deletes admin-action log entries older than <see cref="RetentionMonths"/> months,
/// with one permanent exception: <see cref="AdminActionType.IssueReporterContacted"/> entries are <b>never</b>
/// purged. Those back the "already contacted N times" duplicate-contact warning on the Issue Contact page
/// (<c>IssueContactService.GetContactHistory</c>), so they must survive retention regardless of age.
/// <para><see cref="AdminActionLog"/> is a <c>LogModel</c> (create + hard-delete only) — there is no soft-delete
/// stage, so entries are removed outright, in bounded batches to keep the transaction small on a first run with a
/// large backlog. The retention window is fixed in code (no admin setting). Idempotent; recurring Hangfire job.</para>
/// </summary>
public class AdminLogCleanupJob : IBackgroundJob
{
    /// <summary>Admin-action log entries older than this are purged. Fixed in code (no admin setting).</summary>
    private const int RetentionMonths = 6;

    /// <summary>Bound each delete batch so a first run with a large backlog doesn't build one huge transaction.</summary>
    private const int BatchSize = 250;

    private readonly IRepositoryManager _repos;
    private readonly ILogger<AdminLogCleanupJob> _logger;

    public AdminLogCleanupJob(IRepositoryManager repos, ILogger<AdminLogCleanupJob> logger)
    {
        _repos = repos;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-RetentionMonths);

        var total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // IssueReporterContacted entries are deliberately never purged — they back the duplicate-contact warning.
            var batch = await _repos.GetRepository<AdminActionLog>()
                .AsQueryable()
                .Where(l => l.CreatedUtc < cutoff && l.Action != AdminActionType.IssueReporterContacted)
                .OrderBy(l => l.CreatedUtc)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0) break;

            await _repos.GetRepository<AdminActionLog>().DeleteRangeAsync(batch, cancellationToken: cancellationToken);
            total += batch.Count;

            if (batch.Count < BatchSize) break;   // last (partial) page — nothing more to fetch
        }

        if (total == 0)
            _logger.LogInformation("No admin-action log entries past the {Months}-month retention to purge", RetentionMonths);
        else
            _logger.LogInformation(
                "Admin-log cleanup hard-purged {Count} admin-action log entry(ies) — {Months}-month retention (IssueReporterContacted kept)",
                total, RetentionMonths);
    }
}