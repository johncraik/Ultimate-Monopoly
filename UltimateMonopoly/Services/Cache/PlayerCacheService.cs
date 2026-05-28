using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Services.Cache;

public class PlayerCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly AppDbContext _context;

    private const string CacheKey = "Players";
    private static readonly TimeSpan PlayerNameExpiration = TimeSpan.FromHours(6);

    public PlayerCacheService(IMemoryCache memoryCache,
        AppDbContext context)
    {
        _memoryCache = memoryCache;
        _context = context;
    }
    
    private string GetKey(string userId)
        => $"{CacheKey}__{userId}";

    public async Task<string> GetPlayerName(string userId)
        => await _memoryCache.GetOrCreateAsync(GetKey(userId), async entry =>
        {
            entry.SlidingExpiration = PlayerNameExpiration;
            var player = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            return player == null 
                ? "Unknown Player"
                : string.IsNullOrWhiteSpace(player.DisplayName)
                    ? player.UserName
                    : player.DisplayName;
            
        }) ?? throw new InvalidOperationException("Failed to get default board");
}