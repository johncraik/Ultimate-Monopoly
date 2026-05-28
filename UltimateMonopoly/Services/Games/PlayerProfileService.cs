using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Services.Games;

public class PlayerProfileService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly UserService _userService;

    public PlayerProfileService(IRepositoryManager repos,
        IUserInfo userInfo,
        UserService userService)
    {
        _repos = repos;
        _userInfo = userInfo;
        _userService = userService;
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
}