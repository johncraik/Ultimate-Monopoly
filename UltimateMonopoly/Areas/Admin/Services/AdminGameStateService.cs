using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Services;
using MP.GameEngine.Services.Cards;
using MP.GameEngine.Services.Framework;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.GameEngine;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Rehydrates a game's stored snapshot for a given turn into a <b>read-only</b> <see cref="GameEngine"/> —
/// the same engine the live in-play partials render from, but with no pump, no SignalR, and no persistence.
/// It deserialises the snapshot via <see cref="GameEngineSetupService.SetupGameCache(GameDTO, string, MP.GameEngine.Models.Boards.Board)"/>
/// and wraps it in a fresh <see cref="GameEngine"/>; it is never populated into the live
/// <see cref="GameCacheService"/> nor saved. The admin page sets <c>ViewData["AdminId"]</c> so the reused
/// <c>_PlayerProfileView</c> force-disables every command button.
/// <para>The ctor is <b>not</b> role-guarded: <see cref="BuildEngine"/> is read-only (it never persists), so
/// any caller may rehydrate an engine — including a background job with no user context (the abandoned-games
/// retention job draws via this path). The <b>destructive</b> <see cref="TryRevertToTurn"/> keeps a
/// SystemAdmin guard, applied at the method rather than the ctor.</para>
/// </summary>
public class AdminGameStateService
{
    private readonly ILogger<AdminGameStateService> _logger;
    private readonly IRepositoryManager _repos;
    private readonly GameCacheService _gameCache;
    private readonly GameEngineSetupService _engineSetup;
    private readonly ISnapshotService _snapshotService;
    private readonly IEngineNotifier _notifier;
    private readonly IShortfallService _shortfallService;
    private readonly CardService _cardService;
    private readonly IUserInfo _userInfo;

    public AdminGameStateService(ILogger<AdminGameStateService> logger,
        IRepositoryManager repos,
        GameCacheService gameCache,
        GameEngineSetupService engineSetup,
        ISnapshotService snapshotService,
        IEngineNotifier notifier,
        IShortfallService shortfallService,
        CardService cardService,
        IUserInfo userInfo)
    {
        _logger = logger;
        _repos = repos;
        _gameCache = gameCache;
        _engineSetup = engineSetup;
        _snapshotService = snapshotService;
        _notifier = notifier;
        _shortfallService = shortfallService;
        _cardService = cardService;
        _userInfo = userInfo;
    }

    /// <summary>Builds a read-only engine over the given turn's snapshot, or null if the game / turn / snapshot is missing.</summary>
    public async Task<GameEngine?> BuildEngine(string gameId, uint? turnNumber = null)
    {
        var game = await _repos.GetRepository<UltimateMonopoly.Models.DataModels.Games.Game>()
            .AsQueryable().AsNoTracking()
            .Include(g => g.Turns)
            .FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return null;

        //Grabs specified turn, or the current turn if unspecified
        var turn = game.Turns?.FirstOrDefault(t => t.GameId == gameId 
                                                  && (turnNumber == null 
                                                      ? t.IsCurrentTurn(game.Turns) 
                                                      : t.TurnNumber == turnNumber));
        if (turn == null) return null;

        var snapshot = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable().AsNoTracking()
            .FirstOrDefaultAsync(s => s.TurnId == turn.Id);
        if (snapshot == null) return null;

        var board = await _gameCache.GetGameBoard(game.UserId, game.BoardId);
        if (board == null) return null;

        var gameDto = new GameDTO(game.Id, game.Name, game.BoardId, game.RoundingRule,
            game.UserId, game.State, game.Outcome);

        var cache = _engineSetup.SetupGameCache(gameDto, snapshot.StateJson, board);
        return new GameEngine(cache, _snapshotService, _notifier, _shortfallService, _cardService);
    }

    public async Task<bool> TryRevertToTurn(string gameId, uint turnNumber)
    {
        // The guard lives here (not the ctor) so BuildEngine stays open to any caller; revert hard-deletes
        // turns/snapshots/events, so it is SystemAdmin-only.
        if (!_userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");

        var game = await _repos.GetRepository<UltimateMonopoly.Models.DataModels.Games.Game>()
            .AsQueryable()
            .Include(g => g.Turns)
            .FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(g => g.Id == gameId && g.State == GameState.InPlay);
        if (game == null) return false;

        // The target turn N is the resume point: its GameTurn + GameSnapshot stay, so the game rehydrates
        // at the START of turn N and re-plays it.
        var targetTurn = game.Turns?.FirstOrDefault(t => t.TurnNumber == turnNumber);
        if (targetTurn == null) return false;

        var laterTurns = game.Turns?
            .Where(t => t.TurnNumber > turnNumber)
            .ToList() ?? [];
        if (laterTurns.Count == 0) return false;

        var laterTurnIds = laterTurns.Select(t => t.Id).ToList();

        var laterSnapshots = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable()
            .Where(s => s.GameId == game.Id && laterTurnIds.Contains(s.TurnId))
            .ToListAsync();

        // Events: delete the later turns' events AND the target turn's OWN events. Ending the replayed
        // turn N re-inserts GameTurnEvents(turn N) — TurnStateProvider writes the per-turn events under the
        // turn being ended (its CurrentTurnId), and the GameTurn we keep reuses that same id. So the stale
        // row from the discarded timeline must go first, or the next End Turn duplicate-keys on the
        // GameTurnEvents PK (TurnId). The target's GameTurn + GameSnapshot are deliberately kept.
        var eventTurnIds = laterTurnIds.Append(targetTurn.Id).ToList();
        var staleEvents = await _repos.GetRepository<GameTurnEvents>()
            .AsQueryable()
            .Where(e => e.GameId == game.Id && eventTurnIds.Contains(e.TurnId))
            .ToListAsync();

        await _repos.BeginTransactionAsync();
        try
        {
            if(laterSnapshots.Count > 0)
                await _repos.GetRepository<GameSnapshot>()
                    .DeleteRangeAsync(laterSnapshots, false);

            if(staleEvents.Count > 0)
                await _repos.GetRepository<GameTurnEvents>()
                    .DeleteRangeAsync(staleEvents, false);

            await _repos.GetRepository<GameTurn>()
                .DeleteRangeAsync(laterTurns, false);
            
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Unable to revert game {gameId} to turn {turnNumber}", game.Id, turnNumber);
            return false;
        }
    }
}