using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.ViewModels.Games;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.GameEngine;

namespace UltimateMonopoly.Services.Games;

public class GameService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly IGameEngineFactory _engineFactory;
    private readonly IGameExecutor _executor;
    private readonly GameCacheService _cacheService;
    private readonly IEngineNotifier _notifier;
    private readonly ILogger<GameService> _logger;

    public GameService(IRepositoryManager repos,
        IUserInfo userInfo,
        IGameEngineFactory engineFactory,
        IGameExecutor executor,
        GameCacheService cacheService,
        IEngineNotifier notifier,
        ILogger<GameService> logger)
    {
        _repos = repos;
        _userInfo = userInfo;
        _engineFactory = engineFactory;
        _executor = executor;
        _cacheService = cacheService;
        _notifier = notifier;
        _logger = logger;
    }

    private IQueryable<Game> QueryGames(bool asNoTracking, bool includePlayers, bool includeBoardSkin, bool includeTurns,
        bool includeSnapshots, bool? joinedGames, GameState? state, bool isAdmin = false)
    {
        var query = _repos.GetRepository<Game>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive);

        if (asNoTracking)
            query = query.AsNoTracking();

        if (includePlayers)
            query = query.Include(g => g.Players);

        if (includeBoardSkin)
            query = query.Include(g => g.BoardSkin)
                .ThenInclude(bs => bs!.Spaces);
        
        if (includeTurns)
            query = query.Include(g => g.Turns);
        
        if (includeSnapshots)
            query = query.Include(g => g.Snapshots);
        
        if(state.HasValue)
            query = query.Where(g => g.State == state);
        
        if(joinedGames.HasValue)
            query = joinedGames.Value
                ? query.Where(g => g.CreatedById != _userInfo.UserId) 
                : query.Where(g => g.CreatedById == _userInfo.UserId);

        if (!isAdmin)
            query = query.Where(g => g.Players.Any(p => p.UserId == _userInfo.UserId));
        
        return query.OrderByDescending(g => g.CreatedUtc);
    }

    public async Task<List<GameViewModel>> GetAllMyGames(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetMyGames(asNoTracking, includeTurns, includeSnapshots, null);

    public async Task<List<GameViewModel>> GetMySetupGames(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetMyGames(asNoTracking, includeTurns, includeSnapshots, GameState.Setup);

    public async Task<List<GameViewModel>> GetMyActiveGames(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetMyGames(asNoTracking, includeTurns, includeSnapshots, GameState.InPlay);
    
    public async Task<List<GameViewModel>> GetMyCompletedGames(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetMyGames(asNoTracking, includeTurns, includeSnapshots, GameState.Finished);

    public async Task<List<GameViewModel>> GetMyCancelledGames(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetMyGames(asNoTracking, includeTurns, includeSnapshots, GameState.Cancelled);
    
    private async Task<List<GameViewModel>> GetMyGames(bool asNoTracking, bool includeTurns, bool includeSnapshots, GameState? state)
        => await QueryGames(asNoTracking, true, true, includeTurns, includeSnapshots, false, state)
            .Select(g => new GameViewModel(g, _userInfo.UserId))
            .ToListAsync();
    
    
    public async Task<List<GameViewModel>> GetAllGamesJoined(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetGamesJoined(asNoTracking, includeTurns, includeSnapshots, null);

    public async Task<List<GameViewModel>> GetSetupGamesJoined(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetGamesJoined(asNoTracking, includeTurns, includeSnapshots, GameState.Setup);

    public async Task<List<GameViewModel>> GetActiveGamesJoined(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetGamesJoined(asNoTracking, includeTurns, includeSnapshots, GameState.InPlay);
    
    public async Task<List<GameViewModel>> GetCompletedGamesJoined(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetGamesJoined(asNoTracking, includeTurns, includeSnapshots, GameState.Finished);

    public async Task<List<GameViewModel>> GetCancelledGamesJoined(bool asNoTracking = true, bool includeTurns = true,
        bool includeSnapshots = false)
        => await GetGamesJoined(asNoTracking, includeTurns, includeSnapshots, GameState.Cancelled);
    
    private async Task<List<GameViewModel>> GetGamesJoined(bool asNoTracking, bool includeTurns, bool includeSnapshots, GameState? state)
        => await QueryGames(asNoTracking, true, true, includeTurns, includeSnapshots, true, state)
            .Select(g => new GameViewModel(g, _userInfo.UserId))
            .ToListAsync();


    public async Task<bool> CheckUserInGame(string gameId, string userId)
        => await _repos.GetRepository<GamePlayer>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(p => !p.Game.IsDeleted && p.GameId == gameId && p.UserId == userId);


    public void EnqueueTurn(string gameId, string submittingUserId)
    {
        // Kick off the first player's turn on the game's single-writer executor.
        // The work item resolves the orchestrator in its own scope and runs off
        // this thread; the dice prompt it opens reaches clients via the notifier
        // broadcast and the GamePlayHub.GetCurrentPrompt pull on connect.
        _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            // Authoritative gate re-check on the writer thread. The hub's pre-check
            // can go stale before this item runs (another queued item may have moved
            // the turn on), so a no-longer-valid command no-ops here rather than
            // running into the engine and throwing. See web-orchestration.md §5.
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanStartTurn(current.PlayerId, submittingUserId))
                return;

            var orchestrator = sp.GetRequiredService<MP.GameEngine.Services.PlayerTurnOrchestrator>();
            await orchestrator.StartPlayerTurn(engine, ct);
            await orchestrator.ResolveThirdDieMovement(engine, ct);
        });
    }

    public void EnqueueEndTurn(string gameId, string submittingUserId)
    {
        _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            // Authoritative gate re-check on the writer thread (see EnqueueTurn).
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanEndTurn(current.PlayerId, submittingUserId))
                return;

            var orchestrator = sp.GetRequiredService<MP.GameEngine.Services.PlayerTurnOrchestrator>();
            await orchestrator.EndPlayerTurn(engine, ct);
        });
    }

    public void EnqueueDrawGame(string gameId, string submittingUserId)
    {
        _executor.Enqueue(gameId, async (engine, sp, _) =>
        {
            if(engine.Cache.HostPlayerId != submittingUserId)
                return;
            
            var completionService = sp.GetRequiredService<IGameCompletionService>();
            await completionService.DrawGame(engine);
        });
    }

    /// <summary>
    /// Host-only: tells every connected client to hard-reload and re-fetch the current live
    /// state (the host "Force Refresh" control). Unlike the other host actions this is NOT
    /// enqueued — it broadcasts directly via the notifier, off the turn pump, so it still fires
    /// when the pump is parked on a prompt or wedged (the very situation it recovers from).
    /// Returns false when the caller isn't the host.
    /// </summary>
    public async Task<bool> ForceRefresh(string gameId, string submittingUserId)
    {
        var engine = await _engineFactory.GetAsync(gameId);
        if (engine.Cache.HostPlayerId != submittingUserId)
            return false;

        _notifier.ForceRefresh(gameId);
        return true;
    }

    public async Task ForceRefreshAsAdmin(string gameId)
    {
        //No checks needed
        _notifier.ForceRefresh(gameId);
    }

    /// <summary>
    /// Resets a game's live runtime after an out-of-band DB change (e.g. an admin turn-revert): evicts the
    /// cached working copy, stops the pump (fire-and-forget — it may be mid-work or wedged), and tells
    /// connected clients to reload. The next access then rehydrates from the <i>current</i> snapshot rather
    /// than now-stale in-memory state — without this, a reverted in-play game keeps its old cache/pump and
    /// duplicate-keys on the next snapshot write. Mirrors the cancel path's "tear down after the DB change".
    /// </summary>
    public void ResetRuntimeAsAdmin(string gameId)
    {
        _cacheService.Invalidate(gameId);
        _ = _executor.StopAsync(gameId).AsTask();
        _notifier.ForceRefresh(gameId);
    }


    public async Task<bool> GameInPlay(string gameId)
        => await QueryGames(true, false, false, false, 
            false, null, GameState.InPlay)
            .AnyAsync(g => g.Id == gameId);
    
    public async Task<Game?> GetFinishedGame(string gameId)
        => await QueryGames(true, true, true, true, 
                false, null, GameState.Finished)
            .FirstOrDefaultAsync(g => g.Id == gameId);

    public async Task<bool> TryCancelGame(string gameId, bool isAdmin = false)
    {
        //Get the game as tracking, no includes, created only (no joined games):
        var game = await QueryGames(false, false, false, false, 
                false, isAdmin ? null : false, null, isAdmin)
            .FirstOrDefaultAsync(g => g.Id == gameId && (g.State == GameState.Setup || g.State == GameState.InPlay));
        if(game is null)
            return false;

        var clearInPlayGame = game.State == GameState.InPlay;
        
        var result = game.CancelGame(isAdmin);
        if(!result) return false;
        
        await _repos.GetRepository<Game>()
            .UpdateAsync(game);
        
        if(!clearInPlayGame) return true;

        //Game was in play — tear down the live runtime now its cancellation is committed:
        //evict the cache and stop the pump (mirrors GameCompletionService.ClearGameRuntime).
        //The pump stop is fire-and-forget — it may be mid-work or wedged, and we must not
        //block the cancel on it.
        _cacheService.Invalidate(gameId);
        _ = _executor.StopAsync(gameId).AsTask();
        return true;
    }

    public async Task<bool> TryDeleteGame(string gameId, bool isAdmin = false)
    {
        var game = await QueryGames(false, true, false, false, 
                false, isAdmin ? null : false, isAdmin ? null : GameState.Cancelled, isAdmin)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if(game is null)
            return false;
        
        if(isAdmin && game.State != GameState.Cancelled && game.State != GameState.Finished && game.State != GameState.Finished)
            return false; 
        
        var players = game.Players.ToList();
        var turns = await _repos.GetRepository<GameTurn>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(t => t.GameId == gameId)
            .ToListAsync();
        var snapshots = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(s => s.GameId == gameId)
            .ToListAsync();
        var turnEvents = await _repos.GetRepository<GameTurnEvents>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(s => s.GameId == gameId)
            .ToListAsync();

        await _repos.BeginTransactionAsync();
        try
        {
            if(snapshots.Count > 0)
                await _repos.GetRepository<GameSnapshot>()
                    .SoftDeleteRangeAsync(snapshots, saveNow: false);
            
            if(turnEvents.Count > 0)
                await _repos.GetRepository<GameTurnEvents>()
                    .SoftDeleteRangeAsync(turnEvents, saveNow: false);
            
            if(turns.Count > 0)
                await _repos.GetRepository<GameTurn>()
                    .SoftDeleteRangeAsync(turns, saveNow: false);
            
            if(players.Count > 0)
                await _repos.GetRepository<GamePlayer>()
                    .SoftDeleteRangeAsync(players, saveNow: false);
            
            await _repos.GetRepository<Game>()
                .SoftDeleteAsync(game, saveNow: false);
            
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to delete game {GameId}", gameId);
            return false;
        }
    }
}