using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Services.SubSystems;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.GameEngine;
using EngineRuntime = MP.GameEngine.Services.Framework.GameEngine;

namespace UltimateMonopoly.Services.Games;

public class PlayerProfileService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly UserService _userService;
    private readonly IGameExecutor _executor;

    public PlayerProfileService(IRepositoryManager repos,
        IUserInfo userInfo,
        UserService userService,
        IGameExecutor executor)
    {
        _repos = repos;
        _userInfo = userInfo;
        _userService = userService;
        _executor = executor;
    }


    private IQueryable<GamePlayer> QueryGamePlayers(string gameId, GameState state, bool includeGame, bool includeBoardSkin)
    {
        var query = _repos.GetRepository<GamePlayer>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive);

        switch (includeGame)
        {
            case true when includeBoardSkin:
                query = query.Include(p => p.Game)
                    .ThenInclude(g => g.BoardSkin);
                break;
            case true:
                query = query.Include(p => p.Game);
                break;
        }
        
        return query.Where(p => p.GameId == gameId && p.Game.State == state);
    }

    public async Task<GamePlayer?> GetPlayerForGameSetup(string gameId, string userId)
        => await QueryGamePlayers(gameId, GameState.Setup, true, false)
            .FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<GamePlayer?> GetPlayerForGamePlay(string gameId, string userId)
        => await QueryGamePlayers(gameId, GameState.InPlay, true, false)
            .FirstOrDefaultAsync(p => p.UserId == userId);


    // ─── Portfolio commands ──────────────────────────────────────────────
    // Player-initiated property actions. Each enqueues onto the game's
    // single-writer executor; the engine command (PropertyService.Try…)
    // self-validates and opens its AcquirePropertyPrompt confirmation, parking
    // the work item until the player answers. The acting player is always the
    // current player (portfolio commands are current-player-only); host-bypass
    // lets the host drive it on a player's behalf. See web-orchestration.md §3.

    public void EnqueueMortgage(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.TryMortgageProperty(engine, boardIndex, ct));

    public void EnqueueUnmortgage(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.TryUnmortgageProperty(engine, boardIndex, ct));

    public void EnqueueUnReserve(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.TryUnReserveProperty(engine, boardIndex, ct));

    private void EnqueuePortfolioCommand(string gameId, string submittingUserId,
        Func<PropertyService, EngineRuntime, CancellationToken, Task> command)
    {
        _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            // Authoritative gate re-check on the writer thread (web-orchestration.md §5):
            // the hub's pre-check can go stale before this item runs, so a no-longer-valid
            // command no-ops here rather than running into the engine. Portfolio commands
            // act on the current player; CanPortfolioCommand is host-bypass aware.
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanPortfolioCommand(current.PlayerId, submittingUserId))
                return;

            var properties = sp.GetRequiredService<PropertyService>();
            await command(properties, engine, ct);
        });
    }
}