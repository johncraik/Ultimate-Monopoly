using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MP.GameEngine.Models;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Services;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Services.Cache;

public class GameCacheService
{
    private readonly IMemoryCache _cache;
    private readonly IRepositoryManager _repos;
    private readonly BoardCacheService _boardCache;
    private readonly GameEngineSetupService _engineSetup;

    private const string CacheKey = "GameCache";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(12);

    public GameCacheService(IMemoryCache cache,
        IRepositoryManager repos,
        BoardCacheService boardCache,
        GameEngineSetupService engineSetup)
    {
        _cache = cache;
        _repos = repos;
        _boardCache = boardCache;
        _engineSetup = engineSetup;
    }

    private string GetKey(string gameId) => $"{CacheKey}__{gameId}";

    public async Task<GameCacheModel?> GetGame(string gameId)
    {
        if (_cache.TryGetValue(GetKey(gameId), out GameCacheModel? cached))
            return cached;

        return await HydrateAsync(gameId);
    }

    public void PopulateGame(GameCacheModel game)
    {
        if (game == null!) return;

        _cache.Set(GetKey(game.GameId), game, CacheExpiration);
    }

    /// <summary>
    /// Drops the cached working copy for a game so the next <see cref="GetGame"/>
    /// re-hydrates from the latest snapshot in the database. Used to recover from
    /// a work item that threw mid-turn and may have left the working copy dirty.
    /// </summary>
    public void Invalidate(string gameId) => _cache.Remove(GetKey(gameId));

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

        var board = string.IsNullOrEmpty(game.BoardId)
            ? await _boardCache.GetDefaultBoard()
            : (await _boardCache.GetAllBoards()).FirstOrDefault(b => b.BoardId == game.BoardId);
        if (board is null) return null;

        var gameDto = new GameDTO(game.Id, game.Name, game.BoardId, game.RoundingRule,
            game.UserId, game.State, game.Outcome);

        var cache = _engineSetup.SetupGameCache(gameDto, snapshot.StateJson, board);
        _cache.Set(GetKey(gameId), cache, CacheExpiration);
        return cache;
    }
}