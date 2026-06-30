using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Statistics;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Pages.Games;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Friends;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Pages.Leaderboard;

public class Compare : PageModel
{
    private readonly GameStatsService _statsService;
    private readonly PlayerCacheService _playerCacheService;
    private readonly BoardCacheService _boardCache;
    private readonly FriendService _friendService;
    private readonly BlockAndReportService _blockAndReportService;
    private readonly IUserInfo _userInfo;

    public Compare(GameStatsService statsService,
        PlayerCacheService playerCacheService,
        BoardCacheService boardCache,
        FriendService friendService,
        BlockAndReportService blockAndReportService,
        IUserInfo userInfo)
    {
        _statsService = statsService;
        _playerCacheService = playerCacheService;
        _boardCache = boardCache;
        _friendService = friendService;
        _blockAndReportService = blockAndReportService;
        _userInfo = userInfo;
    }

    public string PlayerName { get; private set; } = "Unknown";
    
    public List<CompareModel.ComparePlayer> AverageComparePlayers { get; private set; } = [];
    public List<CompareModel.ComparePlayer> TotalComparePlayers { get; private set; } = [];
    public List<CompareModel.ComparePlayer> MaxComparePlayers { get; private set; } = [];
    public List<CompareModel.ComparePlayer> MinComparePlayers { get; private set; } = [];
    public Board Board { get; private set; }

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        var friends = await _friendService.AreFriends(userId, _userInfo.UserId);
        if(!friends) return NotFound();
        
        var blocked = await _blockAndReportService.CheckIfBlocksExist(_userInfo.UserId, [userId]);
        if(blocked) return NotFound();

        Board = await _boardCache.GetDefaultBoard();
        
        var userProfile = await _playerCacheService.GetPlayerProfile(_userInfo.UserId);
        var compareProfile = await _playerCacheService.GetPlayerProfile(userId);
        PlayerName = compareProfile.DisplayName;
        
        //Average Stats:
        var avgUS = await _statsService.GetPlayerAvgStatistics(_userInfo.UserId);
        var avgCS = await _statsService.GetPlayerAvgStatistics(userId);
        if(avgUS is null || avgCS is null) return NotFound();

        var avgUserStats = new PlayerGameStat(string.Empty, _userInfo.UserId, avgUS.Value.AllGames);
        var avgCompareStats = new PlayerGameStat(string.Empty, userId, avgCS.Value.AllGames);

        AverageComparePlayers =
        [
            new CompareModel.ComparePlayer(userProfile, null, avgUserStats),
            new CompareModel.ComparePlayer(compareProfile, null, avgCompareStats)
        ];
        
        
        //Total Stats:
        var totUS = await _statsService.GetPlayerTotalStatistics(_userInfo.UserId);
        var totCS = await _statsService.GetPlayerTotalStatistics(userId);
        if(totUS is null || totCS is null) return NotFound();

        var totUserStats = new PlayerGameStat(string.Empty, _userInfo.UserId, totUS.Value.AllGames);
        var totCompareStats = new PlayerGameStat(string.Empty, userId, totCS.Value.AllGames);

        TotalComparePlayers =
        [
            new CompareModel.ComparePlayer(userProfile, null, totUserStats),
            new CompareModel.ComparePlayer(compareProfile, null, totCompareStats)
        ];

        
        //Min Stats:
        var minUS = await _statsService.GetPlayerMinStatistics(_userInfo.UserId);
        var minCS = await _statsService.GetPlayerMinStatistics(userId);
        if(minUS is null || minCS is null) return NotFound();
        
        var minUserStats = new PlayerGameStat(string.Empty, _userInfo.UserId, minUS.Value.AllGames);
        var minCompareStats = new PlayerGameStat(string.Empty, userId, minCS.Value.AllGames);
        
        MinComparePlayers =
        [
            new CompareModel.ComparePlayer(userProfile, null, minUserStats),
            new CompareModel.ComparePlayer(compareProfile, null, minCompareStats)
        ];
        
        
        //Max Stats:
        var maxUS = await _statsService.GetPlayerMaxStatistics(_userInfo.UserId);
        var maxCS = await _statsService.GetPlayerMaxStatistics(userId);
        if(maxUS is null || maxCS is null) return NotFound();
        
        var maxUserStats = new PlayerGameStat(string.Empty, _userInfo.UserId, maxUS.Value.AllGames);
        var maxCompareStats = new PlayerGameStat(string.Empty, userId, maxCS.Value.AllGames);
        
        MaxComparePlayers =
        [
            new CompareModel.ComparePlayer(userProfile, null, maxUserStats),
            new CompareModel.ComparePlayer(compareProfile, null, maxCompareStats)
        ];
        
        return Page();
    }
}