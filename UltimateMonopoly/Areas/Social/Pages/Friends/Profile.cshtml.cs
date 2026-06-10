using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Models.Statistics;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Areas.Social.Pages.Friends;

public class ProfileModel : PageModel
{
    private readonly ProfileService _profile;
    private readonly GameStatsService _gameStats;

    public ProfileModel(ProfileService profile,
        GameStatsService gameStats)
    {
        _profile = profile;
        _gameStats = gameStats;
    }

    public UserProfileViewModel User { get; private set; } = default!;
    
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission)? AvgStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission)? MinStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission)? MaxStats { get; private set; }
    public (PlayerStatRecord Stats, PlayerStatRecord? Comparission)? TotalStats { get; private set; }

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var vm = await _profile.GetUserProfileViewModelAsync(userId);
        if (vm is null) return NotFound();

        User = vm;
        AvgStats = await _gameStats.GetPlayerAvgStatistics(userId);
        MinStats = await _gameStats.GetPlayerMinStatistics(userId);
        MaxStats = await _gameStats.GetPlayerMaxStatistics(userId);
        TotalStats = await _gameStats.GetPlayerTotalStatistics(userId);
        
        return Page();
    }
}
