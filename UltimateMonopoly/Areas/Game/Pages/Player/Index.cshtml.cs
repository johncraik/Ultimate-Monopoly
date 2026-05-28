using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Areas.Game.Pages.Player;

public class Index : PageModel
{
    private readonly PlayerProfileService _playerProfiles;
    private readonly ProfileService _profiles;
    private readonly GameService _gameService;
    private readonly IUserInfo _userInfo;

    public Index(PlayerProfileService playerProfiles,
        ProfileService profiles,
        GameService gameService,
        IUserInfo userInfo)
    {
        _playerProfiles = playerProfiles;
        _profiles = profiles;
        _gameService = gameService;
        _userInfo = userInfo;
    }

    public string GameId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string GameName { get; private set; } = string.Empty;
    public UserProfileViewModel? Profile { get; private set; }
    public ushort? Dice1 { get; private set; }
    public ushort? Dice2 { get; private set; }
    public ushort OrderId { get; private set; }

    /// <summary>True when the signed-in user is viewing their own profile (phone), false when the host views it from the tablet.</summary>
    public bool IsOwnProfile { get; private set; }

    public async Task<IActionResult> OnGetAsync(string gameId, string userId)
    {
        // Any player in the game may view this page — the host tablet views a
        // player's profile alongside the board. The dice-roll prompt itself is
        // still authorised per-response by the hub/validator.
        if (!await _gameService.CheckUserInGame(gameId, _userInfo.UserId))
            return Forbid();

        var player = await _playerProfiles.GetPlayerForGamePlay(gameId, userId);
        if (player is null) return NotFound();

        Profile = await _profiles.GetProfileViewModelAsync(userId);
        if (Profile is null) return NotFound();

        GameId = gameId;
        UserId = userId;
        GameName = player.Game.Name;
        Dice1 = player.Dice1;
        Dice2 = player.Dice2;
        OrderId = player.OrderId;
        IsOwnProfile = userId == _userInfo.UserId;
        return Page();
    }
}