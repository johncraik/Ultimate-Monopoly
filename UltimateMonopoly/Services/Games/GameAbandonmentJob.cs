using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Areas.Admin.Models;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Services.Games;

/// <summary>
/// Abandoned-game management (C1 — Game Settings). Sweeps for <c>InPlay</c> games that have had <b>no new
/// turn</b> for longer than <c>GameSettings.AbandonedRetentionWeeks</c> and resolves each per
/// <c>GameSettings.AbandonedGameAction</c> — either <b>Cancel</b> it (<see cref="GameService.TryCancelGame"/>,
/// admin path) or <b>Draw</b> it (rehydrate a read-only engine via <see cref="AdminGameStateService.BuildEngine"/>
/// and conclude with <see cref="IGameCompletionService.TryDrawGameByAdmin"/> — the same path the admin Game
/// Management "Draw" button uses). Both tear down the game's live runtime and attribute their writes to the
/// System user. Idempotent (a resolved game is no longer <c>InPlay</c>); runs as a recurring Hangfire job.
/// </summary>
public class GameAbandonmentJob : IBackgroundJob
{
    private readonly SettingsDictionary _settings;
    private readonly IRepositoryManager _repos;
    private readonly GameService _gameService;
    private readonly AdminGameStateService _adminGameState;
    private readonly IGameCompletionService _completion;
    private readonly ILogger<GameAbandonmentJob> _logger;

    public GameAbandonmentJob(SettingsDictionary settings,
        IRepositoryManager repos,
        GameService gameService,
        AdminGameStateService adminGameState,
        IGameCompletionService completion,
        ILogger<GameAbandonmentJob> logger)
    {
        _settings = settings;
        _repos = repos;
        _gameService = gameService;
        _adminGameState = adminGameState;
        _completion = completion;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Get();
        if (!settings.EnableAbandonedGamesManagement || settings.AbandonedRetentionWeeks <= 0)
        {
            _logger.LogInformation("Abandoned-game management is disabled — skipping");
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-7 * settings.AbandonedRetentionWeeks);

        // In-play games + their start time (the fallback reference for a game that somehow has no turns yet).
        var inPlay = await _repos.GetRepository<Game>()
            .AsQueryable().AsNoTracking()
            .FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(g => g.State == GameState.InPlay)
            .Select(g => new { g.Id, g.CreatedUtc })
            .ToListAsync(cancellationToken);
        if (inPlay.Count == 0)
        {
            _logger.LogInformation("No in-play games to check for abandonment");
            return;
        }

        // Latest active turn per in-play game (aggregate in the projection so EF emits a real GROUP BY).
        var ids = inPlay.Select(g => g.Id).ToList();
        var lastTurnByGame = (await _repos.GetRepository<GameTurn>()
                .AsQueryable().AsNoTracking()
                .FilterDeleted(DeletedQueryType.OnlyActive)
                .Where(t => ids.Contains(t.GameId))
                .GroupBy(t => t.GameId)
                .Select(g => new { GameId = g.Key, LastTurnUtc = g.Max(t => t.CreatedUtc) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.GameId, x => x.LastTurnUtc);

        // Abandoned = no new turn since the cutoff (fall back to the game's own creation when it has no turns).
        var abandoned = inPlay
            .Where(g => (lastTurnByGame.TryGetValue(g.Id, out var last) ? last : g.CreatedUtc) < cutoff)
            .Select(g => g.Id)
            .ToList();
        if (abandoned.Count == 0)
        {
            _logger.LogInformation("No abandoned in-play games past the {Weeks}-week window", settings.AbandonedRetentionWeeks);
            return;
        }

        var action = settings.AbandonedGameAction;
        var resolved = 0;
        var failed = 0;
        foreach (var gameId in abandoned)
        {
            cancellationToken.ThrowIfCancellationRequested();   // honoured only when ExecutionTimeout is configured

            // Isolate per-game failures so one bad game doesn't abort the sweep (partial-batch processing).
            try
            {
                var ok = action == AbandonedGameAction.Draw
                    ? await DrawGame(gameId)
                    : await _gameService.TryCancelGame(gameId, isAdmin: true);

                if (ok) resolved++;
                else failed++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed to {Action} abandoned game {GameId}",
                    action.ToString().ToLowerInvariant(), gameId);
            }
        }

        _logger.LogInformation("Abandoned-game sweep {Action}ed {Resolved} game(s) ({Failed} failed) — {Weeks}-week window",
            action.ToString().ToLowerInvariant(), resolved, failed, settings.AbandonedRetentionWeeks);
    }

    /// <summary>Concludes an abandoned game as a draw via the admin draw path: rehydrate a read-only engine
    /// from the latest snapshot, then <see cref="IGameCompletionService.TryDrawGameByAdmin"/> (which determines
    /// the outcome from the players left, persists as System, tears down the runtime, and queues the stats
    /// projection). Returns false (logged) if the engine can't be rebuilt.</summary>
    private async Task<bool> DrawGame(string gameId)
    {
        var engine = await _adminGameState.BuildEngine(gameId);
        if (engine == null)
        {
            _logger.LogWarning("Abandoned-game draw skipped — could not rehydrate engine for game {GameId}", gameId);
            return false;
        }

        return await _completion.TryDrawGameByAdmin(engine);
    }
}