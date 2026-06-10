using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models;
using UltimateMonopoly.Data;
using UltimateMonopoly.Enums;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Services.GameEngine;

public class GameCompletionService : IGameCompletionService
{
    private readonly IRepositoryManager _repos;
    private readonly AppDbContext _context;
    private readonly IGameExecutor _executor;
    private readonly GameCacheService _cacheService;
    private readonly IEngineNotifier _notifier;
    private readonly GameStatsService _gameStatsService;
    private readonly ILogger<GameCompletionService> _logger;

    public GameCompletionService(IRepositoryManager repos,
        AppDbContext context,
        IGameExecutor executor,
        GameCacheService cacheService,
        IEngineNotifier notifier,
        GameStatsService gameStatsService,
        ILogger<GameCompletionService> logger)
    {
        _repos = repos;
        _context = context;
        _executor = executor;
        _cacheService = cacheService;
        _notifier = notifier;
        _gameStatsService = gameStatsService;
        _logger = logger;
    }

    public Task DeclareWinner(MP.GameEngine.Services.Framework.GameEngine engine)
        => ConcludeGame(engine.Cache);

    public Task DrawGame(MP.GameEngine.Services.Framework.GameEngine engine)
        => ConcludeGame(engine.Cache);

    private async Task ConcludeGame(GameCacheModel gameCache)
    {
        // Only Players is needed here (outcomes + EndGame). Do NOT eager-include
        // Turns/Snapshots/TurnEvents: four collection includes on one query is a
        // cartesian explosion (Players × Turns × Snapshots × TurnEvents), and each
        // row drags a duplicated StateJson/EventJson blob — hundreds of MB for one
        // game. The stats projection loads those separately, in its own transaction.
        var game = await _repos.GetRepository<Game>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameCache.GameId && g.CreatedById == gameCache.HostPlayerId);
        if(game is null)
            throw new InvalidOperationException("Game not found");
        
        //Stats projection (snapshot + turn events → PlayerGameStat per player) runs in its own
        //transaction, decoupled from finishing the game: it is enqueued as a fire-and-forget
        //Hangfire job AFTER this conclude transaction commits (see below), so a failure to
        //persist stats can't prevent game completion, and the job reads the game as Finished.


        //Tear down the live runtime (cache + pump) now the game is over:
        ClearGameRuntime(gameCache.GameId);


        //Get the players in the game:
        var noneBankrupt = gameCache.Game.GetPlayers(excludePovPlayer: false);
        var bankrupt = gameCache.Game.GetPlayers(true, excludePovPlayer: false);

        //Determine outcome (not from calling method, but from players left):
        var loserIds = bankrupt.Select(p => p.PlayerId).ToList();
        string? winnerId = null;
        List<string>? drawnIds = null;
        GameOutcome outcome;
        switch (noneBankrupt.Count)
        {
            case > 1:
                outcome = GameOutcome.Drawn;
                drawnIds = noneBankrupt.Select(p => p.PlayerId).ToList();
                break;
            case 1:
                outcome = GameOutcome.Winner;
                winnerId = noneBankrupt.First().PlayerId;
                break;
            default:
                throw new InvalidOperationException("No players remaining in game");
        }

        //Update game player's outcomes:
        var beenProcessed = false;
        foreach (var player in game.Players)
        {
            if (player.PlayerGameOutcome != null)
            {
                //This is when we have already declared a winner,
                //so do not update players, OR update AppUser models
                beenProcessed = true;
                break;
            }
            
            var isWinner = winnerId == player.UserId;
            if (isWinner)
            {
                player.PlayerGameOutcome = PlayerGameOutcome.Winner;
                continue;
            }
            
            var isDrawn = drawnIds?.Contains(player.UserId) ?? false;
            if (isDrawn)
            {
                player.PlayerGameOutcome = PlayerGameOutcome.Drawn;
                continue;
            }
            
            var isLoser = loserIds.Contains(player.UserId);
            if (!isLoser) continue;
            
            player.PlayerGameOutcome = PlayerGameOutcome.Loser;
        }
        
        //End the game:
        var res = game.EndGame(outcome);
        if(!res) throw new InvalidOperationException("Failed to end game");

        
        //Update the database
        await _repos.BeginTransactionAsync();
        try
        {
            await _repos.GetRepository<Game>()
                .UpdateAsync(game, IUserInfo.SYSTEM_USER_ID, saveNow: false);

            await _repos.GetRepository<GamePlayer>()
                .UpdateRangeAsync(game.Players, IUserInfo.SYSTEM_USER_ID, saveNow: false);
            
            //Updates stats on AppUser model
            if(!beenProcessed)
                await UpdatePlayerStats(winnerId, drawnIds, loserIds);
            
            //Save changes here will also save changes for AppUser models (same DB)
            //Any failures will rollback the transaction
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to conclude game {GameId}", game.Id);
            throw;
        }

        // The game is now committed as finished — enqueue the stats projection (fire-and-forget;
        // the job reads committed data and writes PlayerGameStat in its own transaction).
        _gameStatsService.ComputeForGame();

        // Tell the connected clients so the in-game pages redirect to the results.
        // Fire-and-forget (the notifier never blocks/throws).
        _notifier.GameCompleted(gameCache.GameId);
    }
    
    
    
    /// <summary>
    /// Tears down the game's live runtime as it concludes: evicts the in-memory cache and
    /// stops its per-game pump. The pump stop is deliberately fire-and-forget — this runs
    /// <i>on</i> that pump's work item, so awaiting <see cref="IGameExecutor.StopAsync"/>
    /// (which awaits the pump task) would deadlock. Leaving it to run in the background tears
    /// the pump down once this work item unwinds.
    /// </summary>
    private void ClearGameRuntime(string gameId)
    {
        _cacheService.Invalidate(gameId);
        _ = _executor.StopAsync(gameId).AsTask();
    }

    
    
    private async Task UpdatePlayerStats(string? winnerId, List<string>? drawnIds, List<string> losersIds)
    {
        if(winnerId != null)
        {
            var winner = await _context.Users.FirstOrDefaultAsync(u => u.Id == winnerId);
            if(winner != null)
                winner.NumberOfWins++;
        }
        
        if(drawnIds != null && drawnIds.Count > 0)
        {
            var drawn = await _context.Users.Where(u => drawnIds.Contains(u.Id)).ToListAsync();
            foreach (var user in drawn)
                user.NumberOfDraws++;
        }
        
        if(losersIds.Count > 0)
        {
            var loosers = await _context.Users.Where(u => losersIds.Contains(u.Id)).ToListAsync();
            foreach (var user in loosers)
                user.NumberOfLosses++;
        }
    }
}