using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UltimateMonopoly.Models.DataModels;

namespace UltimateMonopoly.Services.Cache;

/// <summary>
/// Caches the set of normalised blocked words for the profanity filter. Hydrated from the DB on a
/// cache miss and then held indefinitely (NeverRemove) — the list only changes via an admin edit,
/// which must call <see cref="Invalidate"/>.
/// </summary>
public class BlockedWordsCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private const string CacheKey = "BlockedWords";

    public BlockedWordsCacheService(IMemoryCache memoryCache,
        IRepositoryManager repos,
        IUserInfo userInfo)
    {
        _memoryCache = memoryCache;
        _repos = repos;
        _userInfo = userInfo;
    }

    /// <summary>The active blocked words, by their normalised form. Read on cache miss, then never expires.</summary>
    public async Task<IReadOnlySet<string>> GetBlockedWords()
        => await _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            var words = await _repos.GetRepository<BlockedWord>()
                .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive).AsNoTracking()
                .Select(b => b.NormalisedWord)
                .ToListAsync();
            return (IReadOnlySet<string>)words.ToHashSet();
        }) ?? throw new InvalidOperationException("Failed to load blocked words");

    /// <summary>
    /// Drops the cached set so the next read re-hydrates from the DB — call after editing the blocked-word
    /// table. Admin-only (SystemAdmin); returns false (no-op) for any other caller.
    /// </summary>
    public bool Invalidate()
    {
        if (!_userInfo.IsInRole(SystemRoles.SystemAdmin))
            return false;

        _memoryCache.Remove(CacheKey);
        return true;
    }
}