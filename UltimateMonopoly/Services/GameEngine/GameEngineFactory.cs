using MP.GameEngine.Abstractions;
using UltimateMonopoly.Services.Cache;

namespace UltimateMonopoly.Services.GameEngine;

/// <summary>
/// Builds a <see cref="MP.GameEngine.Services.Framework.GameEngine"/>
/// bundle (cache + the three foundation providers) for a given gameId.
/// The cache itself is fetched from <see cref="GameCacheService"/>, which
/// owns the memory-cache lookup and the DB-hydrate-on-miss path; the
/// factory's only job is to wrap that cache instance in the per-game
/// provider bundle.
/// </summary>
public class GameEngineFactory : IGameEngineFactory
{
    private readonly GameCacheService _cacheService;
    private readonly ISnapshotService _snapshotService;
    private readonly IEngineNotifier _engineNotifier;

    public GameEngineFactory(GameCacheService cacheService,
        ISnapshotService snapshotService,
        IEngineNotifier engineNotifier)
    {
        _cacheService = cacheService;
        _snapshotService = snapshotService;
        _engineNotifier = engineNotifier;
    }

    public async Task<MP.GameEngine.Services.Framework.GameEngine> GetAsync(string gameId)
    {
        var cache = await _cacheService.GetGame(gameId)
            ?? throw new InvalidOperationException(
                $"No active game cache for gameId '{gameId}' — game may not be started, or its snapshot could not be hydrated.");

        return new MP.GameEngine.Services.Framework.GameEngine(cache, _snapshotService, _engineNotifier);
    }
}