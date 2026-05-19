using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Identity.Services;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Areas.Social.Pages.Friends;

public class ProfileModel : PageModel
{
    private readonly ProfileService _profile;

    public ProfileModel(ProfileService profile)
    {
        _profile = profile;
    }

    public UserProfileViewModel User { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var vm = await _profile.GetUserProfileViewModelAsync(userId);
        if (vm is null) return NotFound();

        User = vm;
        return Page();
    }
}
