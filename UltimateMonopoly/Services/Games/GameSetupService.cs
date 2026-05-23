using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using JC.Web.UI.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers.RuleSet;
using UltimateMonopoly.Helpers;
using UltimateMonopoly.Hubs;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.ViewModels;
using UltimateMonopoly.Services.BoardSkins;
using UltimateMonopoly.Services.Friends;

namespace UltimateMonopoly.Services.Games;

public class GameSetupService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly UserService _userService;
    private readonly ILogger<GameSetupService> _logger;
    private readonly BoardSkinService _boardSkinService;
    private readonly UrlLinkService _urlLinkService;
    private readonly BlockAndReportService _blockAndReportService;
    private readonly IHubContext<GameSetupHub> _setupHub;
    private readonly FriendService _friendService;


    public GameSetupService(IRepositoryManager repos,
        IUserInfo userInfo,
        UserService userService,
        ILogger<GameSetupService> logger,
        BoardSkinService boardSkinService,
        UrlLinkService urlLinkService,
        BlockAndReportService blockAndReportService,
        IHubContext<GameSetupHub> setupHub,
        FriendService friendService)
    {
        _repos = repos;
        _userInfo = userInfo;
        _userService = userService;
        _logger = logger;
        _boardSkinService = boardSkinService;
        _urlLinkService = urlLinkService;
        _blockAndReportService = blockAndReportService;
        _setupHub = setupHub;
        _friendService = friendService;
    }


    private async Task ValidateGame(Game game, ModelStateWrapper modelState)
    {
        var existingName = await _repos.GetRepository<Game>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(g => g.CreatedById == _userInfo.UserId 
                           && g.Name.ToLower() == game.Name.ToLower() 
                           && g.Id != game.Id);
        
        if (existingName)
            modelState.AddModelError(nameof(game.Name), "A game already exists with this name");
        
        if (game.Name.Length > 128)
            modelState.AddModelError(nameof(game.Name), "Name must be less than 128 characters");
        
        if (string.IsNullOrWhiteSpace(game.Name))
            modelState.AddModelError(nameof(game.Name), "Name is required");
    }
    
    
    public async Task<GameCreationResult> TryCreateNewGame(ModelStateWrapper modelState, string? name = null,
        string? boardSkinId = null, GameRoundingRule roundingRule = GameRoundingRule.To50)
    {
        var validSkin = true;
        if(!string.IsNullOrEmpty(boardSkinId))
            validSkin = await _boardSkinService.ValidBoardSkin(boardSkinId);
        
        if(!validSkin)
            return new GameCreationResult(false);
        
        var game = new Game(name, boardSkinId, roundingRule);
        await ValidateGame(game, modelState);
        if (!modelState.IsValid) return new GameCreationResult(false);

        string code;
        do { code = JoinCodeGenerator.New(); } 
        while (await _repos.GetRepository<Game>()
                   .AsQueryable()
                   .AnyAsync(g => g.JoinCode == code && g.State == GameState.Setup));
        
        game.SetJoinCode(code);
        var hostPlayer = new GamePlayer(game.Id, _userInfo.UserId);
        
        await _repos.BeginTransactionAsync();
        try
        {
            await _repos.GetRepository<Game>()
                .AddAsync(game, saveNow: false);
            
            await _repos.GetRepository<GamePlayer>()
                .AddAsync(hostPlayer, saveNow: false);
            
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();

        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to create new game for host {UserName} ({UserId})",
                _userInfo.Username, _userInfo.UserId);
            return new GameCreationResult(false);
        }
        
        var link = _urlLinkService.GetUrlLink($"/Game/Setup/{game.Id}");
        var qr = new QrCodeHelper(QrCodeFormat.Svg, 10)
            .GenerateQrCode(link);
        return new GameCreationResult(true, game.Id, qr);
    }


    private async Task<Game?> GetGame(string gameId, bool includePlayers = true, bool includeBoardSkin = false)
    {
        var query = _repos.GetRepository<Game>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive);

        if (includePlayers)
            query = query.Include(g => g.Players);
        
        if (includeBoardSkin)
            query = query.Include(g => g.BoardSkin);
        
        return await query.FirstOrDefaultAsync(g => g.State == GameState.Setup && g.Id == gameId);
    }

    public async Task<Game?> GetSetupGame(string gameId)
    {
        var game = await GetGame(gameId, includePlayers: true, includeBoardSkin: true);
        if (game == null) return null;
        
        return game.CreatedById == _userInfo.UserId ? game : null;
    }


    public async Task<JoinGameResult> TryJoinGame(string gameId, string userId)
    {
        var game = await GetGame(gameId);
        if (game == null) return new JoinGameResult(false, "Cannot find game to join");
        
        return await TryJoinGame(game, userId);
    }

    public async Task<JoinGameResult> TryJonGameFromCode(string joinCode, string userId)
    {
        var game = await _repos.GetRepository<Game>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.JoinCode == joinCode && g.State == GameState.Setup);
        if (game == null) return new JoinGameResult(false, "Cannot find game to join");

        return await TryJoinGame(game, userId);
    }

    private async Task<JoinGameResult> TryJoinGame(Game game, string userId)
    {
        var valid = await _userService.ValidUser(userId);
        if (!valid) return new JoinGameResult(false, "Unable to join game");
        
        var allPlayers = game.Players.Where(p => !p.IsDeleted).ToList();
        if (allPlayers.Any(p => p.UserId == userId))
            return new JoinGameResult(true, GameId: game.Id);
        
        if (allPlayers.Count >= RuleDictionary.MaximumPlayers) 
            return new JoinGameResult(false, "Game is full");
        
        var areFriends = await _friendService.AreFriends(userId, game.CreatedById);
        if (!areFriends) return new JoinGameResult(false, "Unable to join game");
        
        var anyBlocks = await _blockAndReportService.CheckIfBlocksExist(userId, allPlayers.Select(p => p.UserId));
        if (anyBlocks) return new JoinGameResult(false, "Unable to join game");
        
        var player = new GamePlayer(game.Id, userId);
        var orderId = (ushort)(allPlayers.MaxBy(p => p.OrderId)?.OrderId + 1 ?? 0);
        player.SetOrderId(orderId);
        
        await _repos.GetRepository<GamePlayer>()
            .AddAsync(player);

        await _setupHub.Clients.Group(GameSetupHub.GroupName(game.Id))
            .SendAsync("PlayerJoined", userId);
        return new JoinGameResult(true, GameId: game.Id);
    }

    public async Task<bool> TryLeaveGame(string gameId, string userId)
    {
        var game = await GetGame(gameId);
        if (game == null || game.CreatedById == userId) return false;
        
        var valid = await _userService.ValidUser(userId);
        if (!valid) return false;

        return await TryRemovePlayerFromGame(game, userId);
    }


    public async Task<bool> TryKickPlayerFromGame(string gameId, string userId)
    {
        var game = await GetGame(gameId);
        if (game == null || game.CreatedById == userId || game.CreatedById != _userInfo.UserId) 
            return false;
        
        var valid = await _userService.ValidUser(userId);
        if (!valid) return false;

        var removed = await TryRemovePlayerFromGame(game, userId);
        if (removed)
            await _setupHub.Clients.User(userId).SendAsync("Kicked");
        return removed;
    }

    private async Task<bool> TryRemovePlayerFromGame(Game game, string userId)
    {
        var allPlayers = game.Players.Where(p => !p.IsDeleted).ToList();
        var player = allPlayers.FirstOrDefault(p => p.UserId == userId);
        if (player == null) return true; //Not in game, no one to leave/kick
        
        await _repos.BeginTransactionAsync();
        try
        {
            //Hard delete - no need to keep soft deleted records in setup
            await _repos.GetRepository<GamePlayer>()
                .DeleteAsync(player, saveNow: false);
            
            var playersLeft = allPlayers
                .Where(p => p.UserId != userId)
                .OrderBy(p => p.OrderId)
                .ToList();
            var result = await TryReorderPlayers(playersLeft.Select(p => p.UserId), playersLeft, false);
            if (!result) throw new Exception("Failed to reorder players");
            
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();

            await _setupHub.Clients.Group(GameSetupHub.GroupName(game.Id))
                .SendAsync("PlayerLeft", userId);
            return true;
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to kick player from game {GameId} for user {UserId}", game.Id, userId);
            return false;
        }
    }
    
    
    public async Task<bool> TrySetPlayerDiceNumbers(string gameId, string userId, ushort dice1, ushort dice2)
    {
        var validUser = await _userService.ValidUser(userId);
        if (!validUser) return false;
        
        var game = await GetGame(gameId);
        if (game == null) return false;

        if (dice1 < dice2)
            (dice1, dice2) = (dice2, dice1);
        
        var allPlayers = game.Players.Where(p => !p.IsDeleted).ToList();
        var player = allPlayers.FirstOrDefault(p => p.UserId == userId);
        if (player == null) return false;

        var matchingNumbers = allPlayers
            .Where(p => p.UserId != userId)
            .Select(p => new { p.Dice1, p.Dice2 })
            .Any(d => d.Dice1 + d.Dice2 == dice1 + dice2);
        if (matchingNumbers) return false;

        var valid = player.SetDiceNumber(dice1, dice2);
        if (!valid) return false;

        try
        {
            await _repos.GetRepository<GamePlayer>()
                .UpdateAsync(player);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set player dice numbers for game {GameId} and user {UserId}", gameId, userId);
            return false;
        }

        await _setupHub.Clients.Group(GameSetupHub.GroupName(gameId))
            .SendAsync("PlayerDiceSet", userId, dice1, dice2);
        return true;
    }


    public async Task<bool> TryReorderPlayers(string gameId, List<string> playerIds)
    {
        if(playerIds.Count > RuleDictionary.MaximumPlayers)
            return false;
        
        var game = await GetGame(gameId);
        if (game == null) return false;

        var allPlayers = game.Players.Where(p => !p.IsDeleted).ToList();
        if (allPlayers.Count != playerIds.Count || allPlayers.Any(p => !playerIds.Contains(p.UserId)))
            return false;

        var ok = await TryReorderPlayers(playerIds, allPlayers, true);
        if (ok)
            await _setupHub.Clients.Group(GameSetupHub.GroupName(gameId))
                .SendAsync("SeatOrderChanged", playerIds);
        return ok;
    }

    private async Task<bool> TryReorderPlayers(IEnumerable<string> playerIds, IEnumerable<GamePlayer> allPlayers, bool saveNow)
    {
        ushort orderId = 0;
        foreach (var player in playerIds.Select(pid => allPlayers.FirstOrDefault(p => p.UserId == pid)))
        {
            if (player == null) return false;

            var valid = player.SetOrderId(orderId);
            if(!valid) return false;
            
            orderId++;
        }
        
        await _repos.GetRepository<GamePlayer>()
            .UpdateRangeAsync(allPlayers, saveNow: saveNow);
        return true;
    }
}