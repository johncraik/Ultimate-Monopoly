using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Models.Statistics;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Services.Statistics;

public class GameStatsService
{
    private readonly IRepositoryManager _repos;

    public GameStatsService(IRepositoryManager repos)
    {
        _repos = repos;
    }
    
    public async Task ComputeForGame(string gameId)
    {
        //TODO - fire and forget hangfire job using JC.BackgroundServices
    }

    public async Task<PlayerGameStat?> GetPlayerGameStatistics(string gameId, string userId, 
        bool includeGame = true, bool includeGamePlayer = true)
    {
        var query = _repos.GetRepository<PlayerGameStat>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AsNoTracking()
            .Where(x => x.GameId == gameId && x.UserId == userId);
        
        if(includeGame)
            query = query.Include(x => x.Game);
        
        if(includeGamePlayer)
            query = query.Include(x => x.Player);
        
        return await query.FirstOrDefaultAsync();
    }

    public async Task<PlayerStatRecord?> GetPlayerAvgStatistics(string userId)
    {
        var stats = await _repos.GetRepository<PlayerGameStat>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
        
        return null;
    }
}