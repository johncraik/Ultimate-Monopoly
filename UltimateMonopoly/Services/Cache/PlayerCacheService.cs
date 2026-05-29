using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Services.Cache;

public class PlayerCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ProfileService _profileService;

    private const string CacheKey = "Players";
    private static readonly TimeSpan PlayerNameExpiration = TimeSpan.FromHours(6);

    public PlayerCacheService(IMemoryCache memoryCache,
        ProfileService profileService)
    {
        _memoryCache = memoryCache;
        _profileService = profileService;
    }
    
    private string GetKey(string userId)
        => $"{CacheKey}__{userId}";

    public async Task<string> GetPlayerName(string userId)
    {
        var profile = await GetPlayerProfile(userId);
        return profile.DisplayName;
    }

    public async Task<UserProfileViewModel> GetPlayerProfile(string userId)
        => await _memoryCache.GetOrCreateAsync(GetKey(userId), async entry =>
        {
            entry.SlidingExpiration = PlayerNameExpiration;
            return await _profileService.GetUserProfileViewModelAsync(userId);
        }) ?? throw new InvalidOperationException($"Failed to get user/player profile for {userId}");
}