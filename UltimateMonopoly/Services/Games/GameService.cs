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

    public GameService(IRepositoryManager repos,
        IUserInfo userInfo,
        IGameEngineFactory engineFactory,
        IGameExecutor executor)
    {
        _repos = repos;
        _userInfo = userInfo;
        _engineFactory = engineFactory;
        _executor = executor;
    }

    private IQueryable<Game> QueryGames(bool asNoTracking, bool includePlayers, bool includeBoardSkin, bool includeTurns,
        bool includeSnapshots, bool? joinedGames, GameState? state)
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
        
        return query
            .Where(g => g.Players.Any(p => p.UserId == _userInfo.UserId))
            .OrderByDescending(g => g.CreatedUtc);
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


    public async Task<Game?> GetFinishedGame(string gameId)
        => await QueryGames(true, true, true, true, 
                false, null, GameState.Finished)
            .FirstOrDefaultAsync(g => g.Id == gameId);
}