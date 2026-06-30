using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Auditing;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Services.Games;

/// <summary>
/// Game-retention cleanup (C1 — Game Settings; the terminal hard-purge). Permanently (hard) deletes <b>any</b>
/// soft-deleted game-history record — <see cref="GameSnapshot"/>, <see cref="GameTurnEvents"/>,
/// <see cref="GameTurn"/> — whose own <c>DeletedUtc</c> is older than <c>GameSettings.CleanupRetentionMonths</c>.
/// This closes the retention loop: it reclaims both the history of soft-deleted <i>games</i> (cascade-deleted
/// by an admin delete or the cancelled-game job) <b>and</b> the snapshots/events the snapshot auto-delete job
/// soft-deletes on still-active games.
/// <para>The lightweight <see cref="Game"/> / <see cref="GamePlayer"/> rows are <b>not</b> purged even when
/// soft-deleted: <see cref="PlayerGameStat"/> is FK-locked to both and is kept (stats / leaderboard stay
/// intact), so a deleted game's shell lingers as a purged tombstone. Records are purged FK-safe (the leaf
/// snapshots/events before the turns they reference) and in bounded batches. Idempotent; recurring Hangfire job.</para>
/// </summary>
public class GameCleanupJob : IBackgroundJob
{
    // Bound each delete batch so a first run with a large backlog doesn't build one huge transaction.
    private const int BatchSize = 250;

    private readonly SettingsDictionary _settings;
    private readonly IRepositoryManager _repos;
    private readonly ILogger<GameCleanupJob> _logger;

    public GameCleanupJob(SettingsDictionary settings, IRepositoryManager repos, ILogger<GameCleanupJob> logger)
    {
        _settings = settings;
        _repos = repos;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Get();
        if (!settings.EnableCleanup || settings.CleanupRetentionMonths is null or <= 0)
        {
            _logger.LogInformation("Game cleanup is disabled — skipping");
            return;
        }

        var cutoff = DateTime.UtcNow.AddMonths(-settings.CleanupRetentionMonths.Value);

        // FK-safe order: purge the leaf history (snapshots + events) before the turns they reference. The
        // Game / GamePlayer rows are deliberately left (FK-locked by active PlayerGameStat — stats stay intact).
        var snapshots = await PurgeDeleted<GameSnapshot>(cutoff, cancellationToken);
        var events = await PurgeDeleted<GameTurnEvents>(cutoff, cancellationToken);
        var turns = await PurgeDeleted<GameTurn>(cutoff, cancellationToken);

        if (snapshots + events + turns == 0)
        {
            _logger.LogInformation("No soft-deleted game records past the {Months}-month retention to purge",
                settings.CleanupRetentionMonths);
            return;
        }

        _logger.LogInformation("Game cleanup hard-purged {Snapshots} snapshot(s), {Events} event(s), {Turns} turn(s) — {Months}-month retention",
            snapshots, events, turns, settings.CleanupRetentionMonths);
    }

    /// <summary>Hard-deletes soft-deleted <typeparamref name="T"/> records whose <c>DeletedUtc</c> is older than
    /// <paramref name="cutoff"/>, in bounded batches (each batch is its own delete). Returns the total removed.</summary>
    private async Task<int> PurgeDeleted<T>(DateTime cutoff, CancellationToken cancellationToken) where T : AuditModel
    {
        var total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await _repos.GetRepository<T>()
                .AsQueryable().FilterDeleted(DeletedQueryType.OnlyDeleted)
                .Where(x => x.DeletedUtc < cutoff)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0) break;

            await _repos.GetRepository<T>().DeleteRangeAsync(batch, cancellationToken: cancellationToken);
            total += batch.Count;

            if (batch.Count < BatchSize) break;   // last (partial) page — nothing more to fetch
        }
        return total;
    }
}