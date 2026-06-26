using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Areas.Admin.Models;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// The admin surface for Game Settings (C1) — reads/writes the <see cref="GameSettings"/> via the singleton
/// <see cref="SettingsDictionary"/> and triggers a statistics recompute. SystemAdmin-only (Game Management
/// tier). Every mutating action writes an <see cref="AdminLogService"/> entry.
/// </summary>
public class SettingsManagementService
{
    private readonly SettingsDictionary _settings;
    private readonly AdminLogService _adminLog;
    private readonly GameStatsService _gameStats;
    private readonly IRepositoryManager _repos;

    public SettingsManagementService(SettingsDictionary settings,
        AdminLogService adminLog,
        GameStatsService gameStats,
        IRepositoryManager repos,
        IUserInfo userInfo)
    {
        _settings = settings;
        _adminLog = adminLog;
        _gameStats = gameStats;
        _repos = repos;

        if (!userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    public GameSettings Get() => _settings.Get();

    public async Task Save(GameSettings settings)
    {
        await _settings.Update(settings);
        await _adminLog.LogSettingsUpdated(Describe(settings));
    }

    /// <summary>Resets every setting to its in-class default (<c>new GameSettings()</c>).</summary>
    public async Task RevertToDefaults()
    {
        await _settings.Update(new GameSettings());
        await _adminLog.LogSettingsUpdated("reverted game settings to defaults");
    }

    /// <summary>
    /// Hard-deletes every <see cref="PlayerGameStat"/> whose game can be fully recomputed, then re-fires the
    /// (additive) <c>StatisticsJob</c> to rebuild them. A game is eligible <b>only</b> when its history is
    /// intact, so stats that could never be regenerated are preserved:
    /// <list type="bullet">
    ///   <item>the game exists, is active (not soft-deleted), and is <c>Finished</c>;</item>
    ///   <item>it has at least one active snapshot <b>and</b> at least one active event (aggregate level —
    ///   not "none at all" / all hard-deleted);</item>
    ///   <item><b>no</b> snapshot or event record is soft-deleted (a partial history can't be trusted).</item>
    /// </list>
    /// Returns the number of stat records deleted (and re-queued).
    /// </summary>
    public async Task<int> RecomputeStatistics()
    {
        // Candidates: active, Finished games.
        var finishedGameIds = await _repos.GetRepository<UltimateMonopoly.Models.DataModels.Games.Game>()
            .AsQueryable().AsNoTracking()
            .FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(g => g.State == GameState.Finished)
            .Select(g => g.Id)
            .ToListAsync();
        if (finishedGameIds.Count == 0) return 0;

        var snapshots = _repos.GetRepository<GameSnapshot>().AsQueryable().AsNoTracking()
            .Where(s => finishedGameIds.Contains(s.GameId));
        var events = _repos.GetRepository<GameTurnEvents>().AsQueryable().AsNoTracking()
            .Where(e => finishedGameIds.Contains(e.GameId));

        // Games that still have active snapshots / events (excludes "all hard-deleted / none at all").
        var activeSnapshotGameIds = await snapshots.FilterDeleted(DeletedQueryType.OnlyActive)
            .Select(s => s.GameId).Distinct().ToListAsync();
        var activeEventGameIds = await events.FilterDeleted(DeletedQueryType.OnlyActive)
            .Select(e => e.GameId).Distinct().ToListAsync();

        // Games with ANY soft-deleted snapshot / event (a partial history → not recomputable).
        var deletedSnapshotGameIds = await snapshots.FilterDeleted(DeletedQueryType.OnlyDeleted)
            .Select(s => s.GameId).Distinct().ToListAsync();
        var deletedEventGameIds = await events.FilterDeleted(DeletedQueryType.OnlyDeleted)
            .Select(e => e.GameId).Distinct().ToListAsync();

        var tainted = new HashSet<string>(deletedSnapshotGameIds);
        tainted.UnionWith(deletedEventGameIds);

        var eligibleGameIds = activeSnapshotGameIds
            .Intersect(activeEventGameIds)
            .Where(id => !tainted.Contains(id))
            .ToList();
        if (eligibleGameIds.Count == 0) return 0;

        // Hard-delete every stat row (active or soft-deleted) for those games, so the additive job re-adds
        // them — its "already exists" check doesn't filter soft-deleted, so nothing must linger.
        var stats = await _repos.GetRepository<PlayerGameStat>()
            .AsQueryable()
            .Where(s => eligibleGameIds.Contains(s.GameId))
            .ToListAsync();
        if (stats.Count == 0) return 0;

        await _repos.GetRepository<PlayerGameStat>().DeleteRangeAsync(stats);

        // Re-fire the recurring/ad-hoc projection — it rebuilds the now-missing rows for these games.
        _gameStats.ComputeForGame();

        await _adminLog.LogStatisticsRecomputed(stats.Count);
        return stats.Count;
    }

    private static string Describe(GameSettings s)
    {
        var parts = new List<string>
        {
            s.EnableCleanup ? $"cleanup on (after {s.CleanupRetentionMonths} month(s))" : "cleanup off",
            s.EnableAbandonedGamesManagement
                ? $"abandoned games {s.AbandonedGameAction.ToString().ToLowerInvariant()}ed after {s.AbandonedRetentionWeeks} week(s)"
                : "abandoned-game management off",
            s.EnableAutoDeleteCancelled ? $"auto-delete cancelled after {s.AutoDeleteCancelledRetentionMonths} month(s)" : "auto-delete cancelled off",
            s.EnableAutoDeleteSnapshots ? $"auto-delete snapshots after {s.AutoDeleteSnapshotsRetentionMonths} month(s)" : "auto-delete snapshots off"
        };
        return "updated game settings: " + string.Join("; ", parts);
    }
}
