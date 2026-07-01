using System.Text.Json.Nodes;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Pagination;
using JC.Core.Services.DataRepositories;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Areas.Admin.Models;
using UltimateMonopoly.Areas.Admin.Models.ViewModels;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Areas.Admin.Services;

public class GameManagementService
{
    private readonly IRepositoryManager _repos;
    private readonly AdminLogService _adminLogService;
    private readonly UserManagementService _userManagementService;
    private readonly AdminGameStateService _adminGameStateService;
    private readonly IGameCompletionService _gameCompletionService;
    private readonly GameService _gameService;
    private readonly IUserInfo _userInfo;
    private readonly string _defaultBoardName;

    public GameManagementService(IRepositoryManager repos,
        AdminLogService adminLogService,
        UserManagementService userManagementService,
        IConfiguration config,
        AdminGameStateService adminGameStateService,
        IGameCompletionService gameCompletionService,
        GameService gameService,
        IUserInfo userInfo)
    {
        _repos = repos;
        _adminLogService = adminLogService;
        _userManagementService = userManagementService;
        _adminGameStateService = adminGameStateService;
        _gameCompletionService = gameCompletionService;
        _gameService = gameService;
        _userInfo = userInfo;
        _defaultBoardName = config["Imports:BoardName"] ?? "Monopoly Board";
        
        if(!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException(
                "You are not authorized to perform this action."
            );       
    }

    private IQueryable<UltimateMonopoly.Models.DataModels.Games.Game> QueryGames(string? search,
        string? hostIdSearch, string? playerIdSearch, bool asNoTracking, GameState? state, GameOutcome? outcome)
    {
        var query = _repos.GetRepository<UltimateMonopoly.Models.DataModels.Games.Game>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive);
        
        if(asNoTracking)
            query = query.AsNoTracking();
        
        if(state.HasValue)
            query = query.Where(g => g.State == state);
        
        if(outcome.HasValue)
            query = query.Where(g => g.Outcome == outcome);

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(g => g.Name.Contains(search) || g.JoinCode.Contains(search) 
                                                             || g.Players.Any(p => p.UserId.Contains(search)));
        }

        query = string.IsNullOrEmpty(hostIdSearch) switch
        {
            false when !string.IsNullOrEmpty(playerIdSearch)
                => query.Where(g => g.CreatedById == hostIdSearch || g.Players.Any(p => p.UserId == playerIdSearch)),
            false => query.Where(g => g.CreatedById == hostIdSearch),
            true when !string.IsNullOrEmpty(playerIdSearch) => query.Where(g => g.Players.Any(p => p.UserId == playerIdSearch)),
            _ => query
        };
        
        return query.Include(g => g.Turns)
            .Include(g => g.Players)
            .Include(g => g.BoardSkin)
            .OrderByDescending(g => g.CreatedUtc);
    }

    private void AuthCheck()
    {
        if(!_userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    public async Task<PagedList<GameViewModel>> GetGames(int pageNumber, int pageSize, string? search, string? hostIdSearch,
        GameState? state, GameOutcome? outcome, string? playerIdSearch = null)
    {
        AuthCheck();
        
        var games = await QueryGames(search, hostIdSearch, playerIdSearch, true, state, outcome)
            .ToPagedListAsync(pageNumber, pageSize);

        var viewModels = new List<GameViewModel>();
        foreach (var g in games)
        {
            var turn = g.Turns.FirstOrDefault(t => t.IsCurrentTurn(g.Turns))
                ?? new GameTurn(g.Id, g.UserId);
            
            var host = await _userManagementService.GetUserById(g.UserId);
            var players = new List<UserViewModel>();
            foreach (var p in g.Players)
            {
                players.Add(await _userManagementService.GetUserById(p.UserId) ?? new UserViewModel());
            }
            
            viewModels.Add(new GameViewModel(g, turn, host, players));
        }
        
        return new PagedList<GameViewModel>(viewModels, pageNumber, pageSize, games.TotalCount);
    }


    // ---- Details (read-only) ----

    public async Task<GameDetailViewModel?> GetGameDetail(string gameId)
    {
        AuthCheck();
        
        var game = await _repos.GetRepository<UltimateMonopoly.Models.DataModels.Games.Game>()
            .AsQueryable().AsNoTracking()
            .Include(g => g.Players)
            .Include(g => g.Turns)
            .Include(g => g.BoardSkin)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return null;

        var turns = game.Turns.ToList();
        var currentTurn = turns.FirstOrDefault(t => t.IsCurrentTurn(turns))
                          ?? new GameTurn(game.Id, game.UserId);

        var host = await _userManagementService.GetUserById(game.UserId);
        var players = new List<UserViewModel>();
        foreach (var p in game.Players)
            players.Add(await _userManagementService.GetUserById(p.UserId) ?? new UserViewModel());

        var gameVm = new GameViewModel(game, currentTurn, host, players, _defaultBoardName);

        // Per-turn stored sizes — projected as the JSON's character length in SQL (LEN), so the blobs
        // themselves are never transferred. Char length ≈ byte size for the ASCII-dominant JSON.
        var snapshotSizes = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable().AsNoTracking()
            .Where(s => s.GameId == gameId)
            .Select(s => new { s.TurnId, Len = (long)s.StateJson.Length })
            .ToDictionaryAsync(s => s.TurnId, s => s.Len);
        var eventSizes = await _repos.GetRepository<GameTurnEvents>()
            .AsQueryable().AsNoTracking()
            .Where(e => e.GameId == gameId)
            .Select(e => new { e.TurnId, Len = (long)e.EventsJson.Length })
            .ToDictionaryAsync(e => e.TurnId, e => e.Len);

        // Resolve each turn's current-player name once per distinct user, newest turn first.
        var names = new Dictionary<string, UserViewModel?>();
        var rows = new List<GameTurnRowViewModel>();
        foreach (var t in turns.OrderByDescending(t => t.TurnNumber))
        {
            if (!names.TryGetValue(t.UserId, out var user))
            {
                user = await _userManagementService.GetUserById(t.UserId);
                names[t.UserId] = user;
            }
            rows.Add(new GameTurnRowViewModel(t, user,
                snapshotSizes.GetValueOrDefault(t.Id),
                eventSizes.GetValueOrDefault(t.Id)));
        }

        return new GameDetailViewModel(gameVm, rows);
    }


    // ---- Downloads (combined snapshot + events, embedded as nested JSON) ----

    public async Task<GameTurnExport?> BuildTurnExport(string gameId, uint turnNumber)
    {
        AuthCheck();
        
        var turn = await _repos.GetRepository<GameTurn>()
            .AsQueryable().AsNoTracking()
            .FirstOrDefaultAsync(t => t.GameId == gameId && t.TurnNumber == turnNumber);
        if (turn == null) return null;

        var snapshot = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable().AsNoTracking()
            .FirstOrDefaultAsync(s => s.TurnId == turn.Id);
        var events = await _repos.GetRepository<GameTurnEvents>()
            .AsQueryable().AsNoTracking()
            .FirstOrDefaultAsync(e => e.TurnId == turn.Id);

        var name = DisplayName(await _userManagementService.GetUserById(turn.UserId));
        return ToExport(turn, snapshot, events, name);
    }

    public async Task<GameExport?> BuildGameExport(string gameId)
    {
        AuthCheck();
        
        var game = await _repos.GetRepository<UltimateMonopoly.Models.DataModels.Games.Game>()
            .AsQueryable().AsNoTracking()
            .Include(g => g.Turns)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return null;

        var snapshots = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable().AsNoTracking()
            .Where(s => s.GameId == gameId).ToListAsync();
        var events = await _repos.GetRepository<GameTurnEvents>()
            .AsQueryable().AsNoTracking()
            .Where(e => e.GameId == gameId).ToListAsync();

        var names = new Dictionary<string, string?>();
        var turnExports = new List<GameTurnExport>();
        foreach (var t in game.Turns.OrderBy(t => t.TurnNumber))
        {
            if (!names.TryGetValue(t.UserId, out var name))
            {
                name = DisplayName(await _userManagementService.GetUserById(t.UserId));
                names[t.UserId] = name;
            }

            turnExports.Add(ToExport(t,
                snapshots.FirstOrDefault(s => s.TurnId == t.Id),
                events.FirstOrDefault(e => e.TurnId == t.Id),
                name));
        }

        return new GameExport(game.Id, game.Name, game.State.ToString(), game.Outcome.ToString(),
            game.UserId, turnExports);
    }

    private static GameTurnExport ToExport(GameTurn turn, GameSnapshot? snapshot, GameTurnEvents? events, string? playerName)
        => new(turn.TurnNumber, turn.Id, turn.UserId, playerName, turn.IsFinalTurn,
            string.IsNullOrWhiteSpace(snapshot?.StateJson) ? null : JsonNode.Parse(snapshot.StateJson),
            string.IsNullOrWhiteSpace(events?.EventsJson) ? null : JsonNode.Parse(events.EventsJson));

    private static string? DisplayName(UserViewModel? user)
        => user == null || string.IsNullOrWhiteSpace(user.Profile.Username)
            ? null
            : string.IsNullOrWhiteSpace(user.Profile.DisplayName)
                ? user.Profile.Username
                : user.Profile.DisplayName;


    // ---- Actions (state-gated) ----
    // GameService's host actions are player/host-scoped, so each goes through an admin-callable path
    // (TryDrawGameByAdmin / TryCancelGame / TryDeleteGame / ForceRefreshAsAdmin). The GameService call and
    // the AdminActionLog write live together HERE (not the page) so every action is audited. The bool
    // results report whether the action ran (e.g. false when the game is in the wrong state / already gone).

    public async Task<bool> DrawGame(string gameId)
    {
        AuthCheck();
        
        var engine = await _adminGameStateService.BuildEngine(gameId);
        if(engine == null) return false;

        var result = await _gameCompletionService.TryDrawGameByAdmin(engine, isAdmin: true);
        if(!result) return false;

        await _adminLogService.LogGameDrawn(gameId);
        return true;
    }

    public async Task<bool> CancelGame(string gameId)
    {
        AuthCheck();
        
        var result = await _gameService.TryCancelGame(gameId, true);
        if(!result) return false;
        
        await _adminLogService.LogGameCancelled(gameId);
        return true;   
    }

    public async Task<(bool CancelResult, bool DeleteResult)> CancelAndDeleteGame(string gameId)
    {
        AuthCheck();
        
        var result = await CancelGame(gameId);
        return !result 
            ? (false, false) 
            : (true, await DeleteGame(gameId));
    }

    public async Task<bool> DeleteGame(string gameId)
    {
        AuthCheck();
        
        var result = await _gameService.TryDeleteGame(gameId, true);
        if(!result) return false;
        
        await _adminLogService.LogGameDeleted(gameId);
        return true;
    }

    public async Task ForceRefresh(string gameId)
    {
        AuthCheck();
        
        await _gameService.ForceRefreshAsAdmin(gameId);
        await _adminLogService.LogGameRefreshed(gameId);
    }

    // Irreversible: TryRevertToTurn hard-deletes every turn/snapshot/event after turnNumber. Returns false
    // when there's nothing later to delete (turnNumber is already the latest) or the game is gone.
    public async Task<bool> RevertGameToTurn(string gameId, uint turnNumber)
    {
        AuthCheck();
        
        var result = await _adminGameStateService.TryRevertToTurn(gameId, turnNumber);
        if (!result) return false;

        // The DB was reverted under a possibly-live in-play game. Tear down its in-memory runtime so the
        // next access rehydrates from the reverted snapshot — otherwise the stale cache/pump duplicate-keys
        // on the next persist (the reload that "fixed it" was a manual version of this).
        _gameService.ResetRuntimeAsAdmin(gameId);

        await _adminLogService.LogGameReverted(gameId, turnNumber);
        return true;
    }
}