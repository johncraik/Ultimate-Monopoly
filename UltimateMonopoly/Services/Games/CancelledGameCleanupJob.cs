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
/// Auto-delete of cancelled games (C1 — Game Settings). Soft-deletes every active <c>Cancelled</c> game that
/// was cancelled longer ago than <c>GameSettings.AutoDeleteCancelledRetentionMonths</c>, reusing
/// <see cref="GameService.TryDeleteGame"/> (admin path) so the soft-delete + child cascade
/// (snapshots/events/turns/players/game) matches a manual admin delete exactly. The cancel time is taken from
/// <see cref="JC.Core.Models.Auditing.AuditModel.LastModifiedUtc"/> (cancelling is the game's last update),
/// falling back to <c>CreatedUtc</c>.
/// <para>This is the soft-delete stage of the retention pipeline; the later <see cref="GameCleanupJob"/> hard-
/// purges the history once a soft-deleted game ages past its own window. Idempotent (a soft-deleted game drops
/// out of the active set); runs as a recurring Hangfire job.</para>
/// </summary>
public class CancelledGameCleanupJob : IBackgroundJob
{
    private readonly SettingsDictionary _settings;
    private readonly IRepositoryManager _repos;
    private readonly GameService _gameService;
    private readonly ILogger<CancelledGameCleanupJob> _logger;

    public CancelledGameCleanupJob(SettingsDictionary settings,
        IRepositoryManager repos,
        GameService gameService,
        ILogger<CancelledGameCleanupJob> logger)
    {
        _settings = settings;
        _repos = repos;
        _gameService = gameService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Get();
        if (!settings.EnableAutoDeleteCancelled || settings.AutoDeleteCancelledRetentionMonths is null or <= 0)
        {
            _logger.LogInformation("Auto-delete of cancelled games is disabled — skipping");
            return;
        }

        var cutoff = DateTime.UtcNow.AddMonths(-settings.AutoDeleteCancelledRetentionMonths.Value);

        // Active (not already soft-deleted) Cancelled games last modified — i.e. cancelled — before the cutoff.
        var gameIds = await _repos.GetRepository<Game>()
            .AsQueryable().AsNoTracking()
            .FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(g => g.State == GameState.Cancelled
                        && (g.LastModifiedUtc ?? g.CreatedUtc) < cutoff)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        if (gameIds.Count == 0)
        {
            _logger.LogInformation("No cancelled games past the {Months}-month retention to soft-delete",
                settings.AutoDeleteCancelledRetentionMonths);
            return;
        }

        var deleted = 0;
        var failed = 0;
        foreach (var gameId in gameIds)
        {
            cancellationToken.ThrowIfCancellationRequested();   // honoured only when ExecutionTimeout is configured

            // Isolate per-game failures so one bad game doesn't abort the sweep (partial-batch processing).
            try
            {
                if (await _gameService.TryDeleteGame(gameId, isAdmin: true)) deleted++;
                else failed++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed to soft-delete cancelled game {GameId}", gameId);
            }
        }

        _logger.LogInformation("Auto-delete swept {Deleted} cancelled game(s) ({Failed} failed) — {Months}-month retention",
            deleted, failed, settings.AutoDeleteCancelledRetentionMonths);
    }
}