using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Statistics;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Friends;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Areas.Social.Pages.Friends;

public class ProfileModel : PageModel
{
    private readonly ProfileService _profile;
    private readonly GameStatsService _gameStats;
    private readonly BoardCacheService _boardCache;
    private readonly FriendService _friendService;
    private readonly BlockAndReportService _blockAndReport;
    private readonly IUserInfo _userInfo;

    public ProfileModel(ProfileService profile,
        GameStatsService gameStats,
        BoardCacheService boardCache,
        FriendService friendService,
        BlockAndReportService blockAndReport,
        IUserInfo userInfo)
    {
        _profile = profile;
        _gameStats = gameStats;
        _boardCache = boardCache;
        _friendService = friendService;
        _blockAndReport = blockAndReport;
        _userInfo = userInfo;
    }

    public UserProfileViewModel User { get; private set; } = default!;
    
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission, Board board)? AvgStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission, Board board)? MinStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission, Board board)? MaxStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission, Board board)? TotalStats { get; private set; }

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        // Friends-only + block-respecting: a non-friend, or a block either way, gets
        // NotFound — the detailed stat profile is the same gate as the Compare page.
        if (!await _friendService.AreFriends(userId, _userInfo.UserId))
            return NotFound();

        if (await _blockAndReport.CheckIfBlocksExist(_userInfo.UserId, [userId]))
            return NotFound();

        var vm = await _profile.GetUserProfileViewModelAsync(userId);
        if (vm is null) return NotFound();

        User = vm;
        var board = await _boardCache.GetDefaultBoard();
        
        var avg = await _gameStats.GetPlayerAvgStatistics(userId);
        if(avg.HasValue)
            AvgStats = (avg.Value.AllGames, avg.Value.Comparision, board);
        
        var min = await _gameStats.GetPlayerMinStatistics(userId);
        if(min.HasValue)
            MinStats = (min.Value.AllGames, min.Value.Comparision, board);
        
        var max = await _gameStats.GetPlayerMaxStatistics(userId);
        if(max.HasValue)
            MaxStats = (max.Value.AllGames, max.Value.Comparision, board);
        
        var total = await _gameStats.GetPlayerTotalStatistics(userId);
        if(total.HasValue)
            TotalStats = (total.Value.AllGames, total.Value.Comparision, board);
        
        return Page();
    }
}
