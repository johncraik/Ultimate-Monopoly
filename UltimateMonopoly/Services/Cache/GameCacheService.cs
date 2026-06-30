using System.Collections.Concurrent;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Services;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.Imports;

namespace UltimateMonopoly.Services.Cache;

public class GameCacheService
{
    private readonly IMemoryCache _cache;
    private readonly IRepositoryManager _repos;
    private readonly BoardCacheService _boardCache;
    private readonly BoardImportService _boardImportService;
    private readonly GameEngineSetupService _engineSetup;

    private const string CacheKey = "GameCache";
    private const string GameBoardsKey = "GameBoards";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(12);

    // The live working copy is held on a *sliding* window, not the 12h absolute the boards use:
    // every engine work item and every client state-fetch touches it, so an active game keeps
    // resetting the slide and stays warm, while an abandoned game's (potentially large) GameModel
    // is reclaimed ~2h after the last access instead of lingering for 12h. Pump teardown
    // (GameExecutor's sweeper) reclaims the cheap pump sooner; this reclaims the memory.
    private static readonly TimeSpan GameCacheSliding = TimeSpan.FromHours(2);

    private static MemoryCacheEntryOptions GameEntryOptions() =>
        new() { SlidingExpiration = GameCacheSliding };

    // Per-game hydration gate (single-flight, M-02). Static so it is shared across the scoped
    // GameCacheService instances (IMemoryCache is the singleton they share). Entries are bounded by
    // the number of distinct games seen — one tiny SemaphoreSlim each — and left in place rather than
    // racily removed while another caller may be waiting on the gate.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> HydrationLocks = new();

    public GameCacheService(IMemoryCache cache,
        IRepositoryManager repos,
        BoardCacheService boardCache,
        BoardImportService boardImportService,
        GameEngineSetupService engineSetup)
    {
        _cache = cache;
        _repos = repos;
        _boardCache = boardCache;
        _boardImportService = boardImportService;
        _engineSetup = engineSetup;
    }

    private string GetKey(string gameId) => $"{CacheKey}__{gameId}";
    private string GetBoardsKey(string skinId) => $"{GameBoardsKey}__{skinId}";

    public async Task<GameCacheModel?> GetGame(string gameId)
    {
        if (_cache.TryGetValue(GetKey(gameId), out GameCacheModel? cached))
            return cached;

        // Single-flight hydration (M-02): without serialising, two concurrent cache misses for the same
        // game each build and cache a SEPARATE GameCacheModel (the mutable working copy), so the pump
        // could mutate one instance while a hub prompt/state-read holds another — split-brain. Gate per
        // game and double-check the cache inside the lock so only the first miss actually hydrates.
        var gate = HydrationLocks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (_cache.TryGetValue(GetKey(gameId), out cached))
                return cached;

            return await HydrateAsync(gameId);
        }
        finally
        {
            gate.Release();
        }
    }

    public void PopulateGame(GameCacheModel game)
    {
        if (game == null!) return;

        _cache.Set(GetKey(game.GameId), game, GameEntryOptions());

        if(!string.IsNullOrEmpty(game.BoardId))
            _cache.Set(GetBoardsKey(game.BoardId), game.Board, CacheExpiration);
    }

    /// <summary>
    /// Drops the cached working copy for a game so the next <see cref="GetGame"/>
    /// re-hydrates from the latest snapshot in the database. Used to recover from
    /// a work item that threw mid-turn and may have left the working copy dirty.
    /// </summary>
    public void Invalidate(string gameId)
    {
        _cache.Remove(GetKey(gameId));

        // Drop the per-game hydration gate too (M-02) so HydrationLocks doesn't grow unbounded over the
        // server's lifetime — the working copy is gone, so the lock is no longer needed. Not disposed: a
        // SemaphoreSlim used only via WaitAsync/Release allocates no wait handle (nothing to leak), and
        // disposing one a concurrent in-flight hydrate might still hold would throw on its Release().
        HydrationLocks.TryRemove(gameId, out _);
    }

    public async Task SaveChangesAsync(string gameId)
    {
        var game = await GetGame(gameId);
        game?.SaveChanges();
    }

    private async Task<GameCacheModel?> HydrateAsync(string gameId)
    {
        var game = await _repos.GetRepository<Game>()
            .AsQueryable()
            .FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null) return null;

        var snapshot = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable()
            .Where(s => s.GameId == gameId)
            .Include(s => s.Turn)
            .OrderByDescending(s => s.Turn.TurnNumber)
            .FirstOrDefaultAsync();
        if (snapshot is null) return null;

        var board = await GetGameBoard(game.UserId, game.BoardId);
        if (board is null) return null;

        var gameDto = new GameDTO(game.Id, game.Name, game.BoardId, game.RoundingRule,
            game.UserId, game.State, game.Outcome);

        var cache = _engineSetup.SetupGameCache(gameDto, snapshot.StateJson, board);
        _cache.Set(GetKey(gameId), cache, GameEntryOptions());
        return cache;
    }
    
    
    
    //Game Boards (cached boards for in-play games, even when shared board is no longer shared)
    public async Task<Board?> GetGameBoard(string userId, string? skinId = null)
    {
        if(string.IsNullOrEmpty(skinId))
            return await _boardCache.GetDefaultBoard();
        
        return await _cache.GetOrCreateAsync(GetBoardsKey(skinId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheExpiration;
            return await _boardImportService.GetGameBoard(await _boardCache.GetDefaultBoard(), skinId, userId);
        });
    }
}