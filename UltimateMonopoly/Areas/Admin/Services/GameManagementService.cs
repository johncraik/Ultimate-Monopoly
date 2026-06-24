using System.Text.Json.Nodes;
using JC.Core.Extensions;
using JC.Core.Models.Pagination;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Areas.Admin.Models;
using UltimateMonopoly.Areas.Admin.Models.ViewModels;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Areas.Admin.Services;

public class GameManagementService
{
    private readonly IRepositoryManager _repos;
    private readonly AdminLogService _adminLogService;
    private readonly UserManagementService _userManagementService;
    private readonly string _defaultBoardName;

    public GameManagementService(IRepositoryManager repos,
        AdminLogService adminLogService,
        UserManagementService userManagementService,
        IConfiguration config)
    {
        _repos = repos;
        _adminLogService = adminLogService;
        _userManagementService = userManagementService;
        _defaultBoardName = config["Imports:BoardName"] ?? "Monopoly Board";
    }

    private IQueryable<UltimateMonopoly.Models.DataModels.Games.Game> QueryGames(string? search,
        string? hostIdSearch, bool asNoTracking, GameState? state, GameOutcome? outcome)
    {
        var query = _repos.GetRepository<UltimateMonopoly.Models.DataModels.Games.Game>()
            .AsQueryable();
        
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
        
        if(!string.IsNullOrEmpty(hostIdSearch))
            query = query.Where(g => g.CreatedById == hostIdSearch);
        
        return query.Include(g => g.Turns)
            .Include(g => g.Players)
            .Include(g => g.BoardSkin)
            .OrderByDescending(g => g.CreatedUtc);
    }

    public async Task<PagedList<GameViewModel>> GetGames(int pageNumber, int pageSize, string? search, string? hostIdSearch,
        GameState? state, GameOutcome? outcome)
    {
        var games = await QueryGames(search, hostIdSearch, true, state, outcome)
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
            rows.Add(new GameTurnRowViewModel(t, user));
        }

        return new GameDetailViewModel(gameVm, rows);
    }


    // ---- Downloads (combined snapshot + events, embedded as nested JSON) ----

    public async Task<GameTurnExport?> BuildTurnExport(string gameId, uint turnNumber)
    {
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


    // ---- Actions (state-gated) — NOT WIRED YET ----
    // GameService's host actions are player/host-scoped (its QueryGames filters to the caller's own games;
    // ForceRefresh/Draw require the caller to be the host), so they need admin-callable paths first. The
    // GameService call and the AdminActionLog write belong together HERE (not the page) so every action is
    // audited — uncomment each flow once the admin path lands. Returns false (not performed) for now.

    public Task<bool> DrawGame(string gameId)
    {
        // if (!await _gameService.EnqueueDrawGameAsAdmin(gameId)) return false;   // admin draw — skip the host check
        // await _adminLogService.LogGameDrawn(gameId);
        // return true;
        return Task.FromResult(false);
    }

    public Task<bool> CancelGame(string gameId)
    {
        // if (!await _gameService.TryCancelGameAsAdmin(gameId)) return false;     // admin cancel — no player filter
        // await _adminLogService.LogGameCancelled(gameId);
        // return true;
        return Task.FromResult(false);
    }

    public Task<bool> CancelAndDeleteGame(string gameId)
    {
        // if (!await _gameService.TryCancelGameAsAdmin(gameId)) return false;
        // if (!await _gameService.TryDeleteGameAsAdmin(gameId)) return false;     // delete allows {Finished, Cancelled}
        // await _adminLogService.LogGameDeleted(gameId);
        // return true;
        return Task.FromResult(false);
    }

    public Task<bool> DeleteGame(string gameId)
    {
        // if (!await _gameService.TryDeleteGameAsAdmin(gameId)) return false;     // delete allows {Finished, Cancelled}
        // await _adminLogService.LogGameDeleted(gameId);
        // return true;
        return Task.FromResult(false);
    }

    public Task<bool> ForceRefresh(string gameId)
    {
        // if (!await _gameService.ForceRefreshAsAdmin(gameId)) return false;      // skip host check, broadcast via IEngineNotifier
        // return true;   // non-destructive — not audited (no AdminActionType for a refresh)
        return Task.FromResult(false);
    }
}