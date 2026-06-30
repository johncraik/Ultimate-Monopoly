using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Services.Games;

/// <summary>
/// Auto-delete of game snapshots + events (C1 — Game Settings; disabled by default). For Finished / Cancelled
/// games concluded longer ago than <c>GameSettings.AutoDeleteSnapshotsRetentionMonths</c>, <b>soft-deletes</b>
/// the game's <see cref="GameSnapshot"/> + <see cref="GameTurnEvents"/> (the bulky per-turn blobs), keeping the
/// <see cref="Game"/> / <see cref="GamePlayer"/> / <see cref="GameTurn"/> rows.
/// <para><b>Soft-delete only</b> — matching the "AutoDelete = soft, Cleanup = hard" split in
/// <c>GameSettings</c>. The rows are flagged deleted (the game becomes non-recomputable); the StateJson blobs
/// are physically reclaimed later by <see cref="GameCleanupJob"/>, which hard-purges any soft-deleted snapshot
/// / event past the Cleanup retention window.</para>
/// <para><b>Stats guard:</b> a Finished game is purged only once its <see cref="PlayerGameStat"/> rows exist,
/// so the projection's only source is never destroyed before stats were computed. Cancelled games are excluded
/// from stats entirely, so they purge unconditionally. Idempotent; runs as a recurring Hangfire job.</para>
/// </summary>
public class SnapshotCleanupJob : IBackgroundJob
{
    private readonly SettingsDictionary _settings;
    private readonly IRepositoryManager _repos;
    private readonly ILogger<SnapshotCleanupJob> _logger;

    public SnapshotCleanupJob(SettingsDictionary settings,
        IRepositoryManager repos,
        ILogger<SnapshotCleanupJob> logger)
    {
        _settings = settings;
        _repos = repos;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Get();
        if (!settings.EnableAutoDeleteSnapshots || settings.AutoDeleteSnapshotsRetentionMonths is null or <= 0)
        {
            _logger.LogInformation("Auto-delete of snapshots is disabled — skipping");
            return;
        }

        var cutoff = DateTime.UtcNow.AddMonths(-settings.AutoDeleteSnapshotsRetentionMonths.Value);

        // Finished / Cancelled, active games last touched (concluded) before the cutoff.
        var candidates = await _repos.GetRepository<Game>()
            .AsQueryable().AsNoTracking()
            .FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(g => (g.State == GameState.Finished || g.State == GameState.Cancelled)
                        && (g.LastModifiedUtc ?? g.CreatedUtc) < cutoff)
            .Select(g => new { g.Id, g.State })
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            _logger.LogInformation("No finished/cancelled games past the {Months}-month snapshot retention",
                settings.AutoDeleteSnapshotsRetentionMonths);
            return;
        }

        // Finished games: only purge once stats exist (never destroy the projection's only source first).
        // Cancelled games are excluded from stats entirely, so they purge unconditionally.
        var finishedIds = candidates.Where(c => c.State == GameState.Finished).Select(c => c.Id).ToList();
        var finishedWithStats = new HashSet<string>();
        if (finishedIds.Count > 0)
        {
            finishedWithStats = (await _repos.GetRepository<PlayerGameStat>()
                    .AsQueryable().AsNoTracking()
                    .Where(s => finishedIds.Contains(s.GameId))
                    .Select(s => s.GameId).Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var gameIds = candidates
            .Where(c => c.State == GameState.Cancelled || finishedWithStats.Contains(c.Id))
            .Select(c => c.Id)
            .ToList();
        if (gameIds.Count == 0)
        {
            _logger.LogInformation("No eligible games to purge snapshots for (finished games still awaiting stats)");
            return;
        }

        var purgedGames = 0;
        var purgedRecords = 0;
        var failed = 0;
        foreach (var gameId in gameIds)
        {
            cancellationToken.ThrowIfCancellationRequested();   // honoured only when ExecutionTimeout is configured

            // Isolate per-game failures so one bad game doesn't abort the sweep (partial-batch processing).
            try
            {
                var removed = await PurgeSnapshots(gameId, cancellationToken);
                if (removed > 0)
                {
                    purgedGames++;
                    purgedRecords += removed;
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed to soft-delete snapshots for game {GameId}", gameId);
            }
        }

        _logger.LogInformation("Snapshot auto-delete soft-deleted {Records} record(s) across {Games} game(s) ({Failed} failed) — {Months}-month retention",
            purgedRecords, purgedGames, failed, settings.AutoDeleteSnapshotsRetentionMonths);
    }

    /// <summary>Soft-deletes one game's active snapshots + events (attributed to System) in a single
    /// transaction, keeping its turns/players/game. Returns the count soft-deleted (0 if none remained).</summary>
    private async Task<int> PurgeSnapshots(string gameId, CancellationToken cancellationToken)
    {
        var snapshots = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(s => s.GameId == gameId).ToListAsync(cancellationToken);
        var events = await _repos.GetRepository<GameTurnEvents>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(e => e.GameId == gameId).ToListAsync(cancellationToken);

        if (snapshots.Count == 0 && events.Count == 0)
            return 0;

        await _repos.BeginTransactionAsync();
        try
        {
            if (snapshots.Count > 0)
                await _repos.GetRepository<GameSnapshot>()
                    .SoftDeleteRangeAsync(snapshots, IUserInfo.SYSTEM_USER_ID, saveNow: false, cancellationToken: cancellationToken);

            if (events.Count > 0)
                await _repos.GetRepository<GameTurnEvents>()
                    .SoftDeleteRangeAsync(events, IUserInfo.SYSTEM_USER_ID, saveNow: false, cancellationToken: cancellationToken);

            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();
            return snapshots.Count + events.Count;
        }
        catch
        {
            await _repos.RollbackTransactionAsync();
            throw;   // surfaced to the per-game catch above (logged + counted as failed)
        }
    }
}