using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Models.ViewModels.Leaderboard;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Pages.Leaderboard;

/// <summary>
/// The leaderboard page (/Leaderboard): every player ranked best-first by
/// <see cref="LeaderboardService.GetLeaderboard"/>. The view renders the top three as a podium
/// (gold / silver / bronze) and the rest as a ranked list.
/// </summary>
public class IndexModel : PageModel
{
    public const int MinimumGames = 1;
    
    private readonly LeaderboardService _leaderboardService;
    private readonly ProfileService _profile;
    private readonly IUserInfo _userInfo;

    public IndexModel(LeaderboardService leaderboardService, ProfileService profile, IUserInfo userInfo)
    {
        _leaderboardService = leaderboardService;
        _profile = profile;
        _userInfo = userInfo;
    }

    /// <summary>Players ranked best-first (index 0 = #1).</summary>
    public List<LeaderboardRecord> Records { get; private set; } = [];

    /// <summary>The viewing player's id, used to highlight their own row.</summary>
    public string? CurrentUserId { get; private set; }

    public bool ShowLeaderboard { get; private set; }

    /// <summary>Whether the current user is hidden from non-friends (E2). Drives the privacy toggle label.</summary>
    public bool IsHidden { get; private set; }

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public async Task OnGetAsync()
    {
        Records = await _leaderboardService.GetLeaderboard();
        CurrentUserId = _userInfo.UserId;

        if(Records.Select(r => r.UserProfile.UserId).Contains(CurrentUserId))
            ShowLeaderboard = true;

        IsHidden = await _profile.IsHidden();
    }

    public async Task<IActionResult> OnPostToggleVisibilityAsync()
    {
        var hidden = await _profile.IsHidden();
        var ok = hidden ? await _profile.TryUnhideUser() : await _profile.TryHideUser();

        if (ok)
        {
            StatusMessage = hidden
                ? "Your profile is now public — everyone can see your name on the leaderboard."
                : "Your profile is now private — only you and your friends can see your name on the leaderboard.";
            StatusKind = "success";
        }
        else
        {
            StatusMessage = "Could not update your profile visibility.";
            StatusKind = "danger";
        }

        return RedirectToPage();
    }
}