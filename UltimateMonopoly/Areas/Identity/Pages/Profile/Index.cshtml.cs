using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Statistics;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Friends;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Areas.Identity.Pages.Profile;

public class IndexModel : PageModel
{
    private const ushort BlockedPageSize = 50;

    private readonly ProfileService _profile;
    private readonly BlockAndReportService _blockAndReport;
    private readonly GameStatsService _gameStats;
    private readonly BoardCacheService _boardCache;

    public IndexModel(
        ProfileService profile,
        BlockAndReportService blockAndReport,
        GameStatsService gameStats,
        BoardCacheService boardCache)
    {
        _profile = profile;
        _blockAndReport = blockAndReport;
        _gameStats = gameStats;
        _boardCache = boardCache;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "image";

    [BindProperty(SupportsGet = true, Name = "page")]
    public ushort PageNumber { get; set; } = 1;

    public IReadOnlyList<string> AvailableImageNames { get; private set; } = [];

    public PagedList<UserProfileViewModel>? BlockedUsers { get; private set; }

    public UserProfileViewModel? CurrentUser { get; private set; }

    /// <summary>Whether the current user is hidden from non-friends (E2). Drives the profile privacy toggle.</summary>
    public bool IsHidden { get; private set; }

    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission, Board board)? AvgStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission, Board board)? MinStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission, Board board)? MaxStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission, Board board)? TotalStats { get; private set; }
    

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public async Task OnGetAsync()
    {
        Tab = NormaliseTab(Tab);
        var profile = await _profile.GetAsync();
        Input.AvatarColour = profile.AvatarColour;
        Input.AvatarImageName = profile.AvatarImageName;
        AvailableImageNames = _profile.GetAvailableAvatarImageNames();
        CurrentUser = await _profile.GetCurrentUserProfileViewModelAsync();
        
        var board = await _boardCache.GetDefaultBoard();
        if(CurrentUser is not null)
        {
            var avg = await _gameStats.GetPlayerAvgStatistics(CurrentUser.UserId);
            if(avg.HasValue)
                AvgStats = (avg.Value.AllGames, avg.Value.Comparision, board);
            
            var min = await _gameStats.GetPlayerMinStatistics(CurrentUser.UserId);
            if(min.HasValue)
                MinStats = (min.Value.AllGames, min.Value.Comparision, board);
            
            var max = await _gameStats.GetPlayerMaxStatistics(CurrentUser.UserId);
            if(max.HasValue)
                MaxStats = (max.Value.AllGames, max.Value.Comparision, board);
            
            var total = await _gameStats.GetPlayerTotalStatistics(CurrentUser.UserId);
            if(total.HasValue)
                TotalStats = (total.Value.AllGames, total.Value.Comparision, board);
        }

        if (PageNumber < 1) PageNumber = 1;
        BlockedUsers = await _blockAndReport.GetBlockedUsers(PageNumber, BlockedPageSize);

        IsHidden = await _profile.IsHidden();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Tab = NormaliseTab(Tab);

        var ok = await _profile.TryUpdateAsync(
            new UserProfile(Input.AvatarColour, Input.AvatarImageName));

        StatusMessage = ok ? "Profile updated." : "Could not update profile.";
        StatusKind = ok ? "success" : "danger";

        return RedirectToPage(new { tab = Tab });
    }

    public async Task<IActionResult> OnPostClearColourAsync()
    {
        Tab = NormaliseTab(Tab);

        var ok = await _profile.TryUpdateAsync(
            new UserProfile(null, Input.AvatarImageName));

        StatusMessage = ok ? "Avatar colour cleared." : "Could not clear colour.";
        StatusKind = ok ? "success" : "danger";

        return RedirectToPage(new { tab = Tab });
    }

    public async Task<IActionResult> OnPostClearImageAsync()
    {
        Tab = NormaliseTab(Tab);

        var ok = await _profile.TryUpdateAsync(
            new UserProfile(Input.AvatarColour, null));

        StatusMessage = ok ? "Avatar image cleared." : "Could not clear image.";
        StatusKind = ok ? "success" : "danger";

        return RedirectToPage(new { tab = Tab });
    }

    public async Task<IActionResult> OnPostUnblockAsync(string userId)
    {
        var ok = await _blockAndReport.TryUnblockUser(userId);
        StatusMessage = ok ? "User unblocked." : "Could not unblock user.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { tab = "blocked" });
    }

    public async Task<IActionResult> OnPostToggleVisibilityAsync()
    {
        var hidden = await _profile.IsHidden();
        var ok = hidden ? await _profile.TryUnhideUser() : await _profile.TryHideUser();

        StatusMessage = ok
            ? (hidden
                ? "Your profile is now public — everyone can see your name on the leaderboard."
                : "Your profile is now private — only you and your friends can see your name on the leaderboard.")
            : "Could not update your profile visibility.";
        StatusKind = ok ? "success" : "danger";

        return RedirectToPage(new { tab = "stats" });
    }

    public IActionResult OnGetAvatarImage(string name)
    {
        var path = _profile.GetAvatarImagePath(name);
        return path is null
            ? NotFound()
            : PhysicalFile(path, "image/png");
    }

    private static string NormaliseTab(string? tab) => tab switch
    {
        "colour"  => "colour",
        "stats"   => "stats",
        "blocked" => "blocked",
        _         => "image"
    };

    public class InputModel
    {
        public string? AvatarColour { get; set; }
        public string? AvatarImageName { get; set; }
    }
}