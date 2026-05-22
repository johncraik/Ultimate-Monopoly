using Microsoft.Extensions.Caching.Memory;
using MP.GameEngine.Models;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Services.Cache;

public class GameCacheService
{
    private readonly IMemoryCache _cache;
    private readonly GameSnapshotService _snapshotService;

    private const string CacheKey = "GameCache";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(12);
    
    public GameCacheService(IMemoryCache cache,
        GameSnapshotService snapshotService)
    {
        _cache = cache;
        _snapshotService = snapshotService;
    }
    
    private string GetKey(string gameId) => $"{CacheKey}__{gameId}";

    public async Task<GameCacheModel?> GetGame(string gameId)
        => await _cache.GetOrCreateAsync(GetKey(gameId), async entry =>
        {
            entry.SlidingExpiration = CacheExpiration;
            return await _snapshotService.GetGameCacheModel(gameId);
        });

    public async Task SaveChangesAsync(string gameId)
    {
        var game = await GetGame(gameId);
        game?.SaveChanges();
    }
}