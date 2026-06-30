using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.ViewModels.Games;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Friends;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Pages;

/// <summary>
/// The home page (`/`) — an adaptive landing (F1). Anonymous visitors get a public front door (the pitch +
/// sign-up CTAs); signed-in players get a personalised hub — their profile, primary actions, a games / stats
/// snapshot, and quick-resume cards for their in-play games. <c>[AllowAnonymous]</c> so it bypasses the global
/// auth fallback for the landing; the hub branch only runs when the visitor is signed in.
/// <para>(Replaces the old behaviour, which redirected `/` straight to <c>/Games/MyGames</c> and kept a theme
/// component preview behind <c>?bypass=true</c> — that preview now lives at <c>/Dev/Theme</c>.)</para>
/// </summary>
[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly ProfileService _profile;
    private readonly GameService _games;
    private readonly FriendService _friends;

    public IndexModel(SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        ProfileService profile,
        GameService games,
        FriendService friends)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _profile = profile;
        _games = games;
        _friends = friends;
    }

    public bool IsSignedIn { get; private set; }

    /// <summary>The signed-in player's profile (avatar + display name + W/L/D). Null for anonymous visitors.</summary>
    public UserProfileViewModel? CurrentUser { get; private set; }

    /// <summary>In-play games the player can resume — host or joined, deduped, newest first. Hub only.</summary>
    public List<GameViewModel> ActiveGames { get; private set; } = [];

    /// <summary>Games still in setup the player is part of — host or joined. Hub only.</summary>
    public List<GameViewModel> SetupGames { get; private set; } = [];

    /// <summary>Count of incoming friend requests awaiting the player's response. Hub only.</summary>
    public int PendingRequests { get; private set; }

    /// <summary>First-run welcome card: shown until the player dismisses it or has any game (auto-retire). Hub only.</summary>
    public bool ShowWelcome { get; private set; }

    public async Task OnGetAsync()
    {
        IsSignedIn = _signInManager.IsSignedIn(User);
        if (!IsSignedIn)
            return;   // anonymous → render the public landing only

        CurrentUser = await _profile.GetCurrentUserProfileViewModelAsync();

        // Games the player can act on — host OR joined — deduped by id. Turns aren't needed for the cards.
        ActiveGames = Merge(
            await _games.GetMyActiveGames(includeTurns: false),
            await _games.GetActiveGamesJoined(includeTurns: false));
        SetupGames = Merge(
            await _games.GetMySetupGames(includeTurns: false),
            await _games.GetSetupGamesJoined(includeTurns: false));

        var requests = await _friends.GetFriendRequests();
        PendingRequests = requests.IncomingRequests.Count;

        // Welcome the first-run player: not yet dismissed, and they've no games at all (auto-retires
        // once they've played a game or have one in setup/play).
        var appUser = await _userManager.GetUserAsync(User);
        ShowWelcome = appUser is { HasDismissedWelcome: false }
            && (CurrentUser?.NumberOfGamesPlayed ?? 0) == 0
            && ActiveGames.Count == 0
            && SetupGames.Count == 0;
    }

    /// <summary>Flips the welcome flag off for good ("Don't show again"). No-op for anonymous posters.</summary>
    public async Task<IActionResult> OnPostDismissWelcomeAsync()
    {
        var appUser = await _userManager.GetUserAsync(User);
        if (appUser is { HasDismissedWelcome: false })
        {
            appUser.HasDismissedWelcome = true;
            await _userManager.UpdateAsync(appUser);
        }

        return RedirectToPage();
    }

    /// <summary>Unions two game lists (host + joined), keeping the first of any id — a host is also a player.</summary>
    private static List<GameViewModel> Merge(List<GameViewModel> a, List<GameViewModel> b)
        => a.Concat(b)
            .GroupBy(g => g.Id)
            .Select(grp => grp.First())
            .Take(10)
            .ToList();
}