using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Identity.Services;
using UltimateMonopoly.Areas.Social.Services;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Areas.Identity.Pages.Profile;

public class IndexModel : PageModel
{
    private const ushort BlockedPageSize = 50;

    private readonly ProfileService _profile;
    private readonly BlockAndReportService _blockAndReport;

    public IndexModel(
        ProfileService profile,
        BlockAndReportService blockAndReport)
    {
        _profile = profile;
        _blockAndReport = blockAndReport;
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

        if (PageNumber < 1) PageNumber = 1;
        BlockedUsers = await _blockAndReport.GetBlockedUsers(PageNumber, BlockedPageSize);
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