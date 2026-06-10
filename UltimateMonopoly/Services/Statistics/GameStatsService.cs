using JC.BackgroundJobs.Services;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models.Statistics;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Services.Statistics;

public class GameStatsService
{
    private readonly IRepositoryManager _repos;
    private readonly IHangfireScheduler _scheduler;

    public GameStatsService(IRepositoryManager repos, IHangfireScheduler scheduler)
    {
        _repos = repos;
        _scheduler = scheduler;
    }
    
    public void ComputeForGame()
        => _scheduler.Enqueue<StatisticsJob>();


    private IQueryable<PlayerGameStat> QueryStats(string gameId, bool includeGame, bool includeGamePlayer)
    {
        var query = _repos.GetRepository<PlayerGameStat>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AsNoTracking()
            .Where(x => x.GameId == gameId);
        
        if(includeGame)
            query = query.Include(x => x.Game);
        
        if(includeGamePlayer)
            query = query.Include(x => x.Player);
        
        return query;
    }
    
    public async Task<List<PlayerGameStat>> GetGameStatistics(string gameId, bool includeGame = true)
        => await QueryStats(gameId, includeGame, true)
            .ToListAsync();
    
    public async Task<PlayerGameStat?> GetPlayerGameStatistics(string gameId, string userId, 
        bool includeGame = true, bool includeGamePlayer = true)
        => await QueryStats(gameId, includeGame, includeGamePlayer)
            .FirstOrDefaultAsync(x => x.UserId == userId);


    private async Task<(PlayerStatRecord AllGames, PlayerStatRecord? Comparision)?> GetPlayerStatistics(string userId, StatisticView view)
    {
        var stats = await _repos.GetRepository<PlayerGameStat>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Game.CreatedUtc)
            .ToListAsync();

        //No finished games → no aggregate to build (the PlayerStatRecord aggregate ctor
        //averages over the list and would throw on an empty sequence). Callers treat null
        //as "nothing to show yet".
        if (stats.Count == 0)
            return null;

        var comparisionList = new List<PlayerGameStat>();
        if(stats.Count > 1)
            comparisionList = stats[..^1];
        
        var allGames = new PlayerStatRecord(stats.Select(PlayerStatRecord (s) => s).ToList(), view);
        PlayerStatRecord? comparision = null;
        if(comparisionList.Count > 0)
            comparision = new PlayerStatRecord(comparisionList.Select(PlayerStatRecord (s) => s).ToList(), view);
        
        return (allGames, comparision);
    }
    
    public async Task<(PlayerStatRecord AllGames, PlayerStatRecord? Comparision)?> GetPlayerAvgStatistics(string userId)
        => await GetPlayerStatistics(userId, StatisticView.Average);
    
    public async Task<(PlayerStatRecord AllGames, PlayerStatRecord? Comparision)?> GetPlayerMinStatistics(string userId)
        => await GetPlayerStatistics(userId, StatisticView.Min);
    
    public async Task<(PlayerStatRecord AllGames, PlayerStatRecord? Comparision)?> GetPlayerMaxStatistics(string userId)
        => await GetPlayerStatistics(userId, StatisticView.Max);
    
    public async Task<(PlayerStatRecord AllGames, PlayerStatRecord? Comparision)?> GetPlayerTotalStatistics(string userId)
        => await GetPlayerStatistics(userId, StatisticView.Total);
}