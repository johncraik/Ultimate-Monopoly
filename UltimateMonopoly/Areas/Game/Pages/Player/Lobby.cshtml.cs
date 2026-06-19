using JC.Core.Models;
using JC.Web.UI.HTML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Areas.Game.Pages.Player;

public class Lobby : PageModel
{
    private readonly PlayerProfileService _playerProfiles;
    private readonly ProfileService _profiles;
    private readonly GameSetupService _gameSetup;
    private readonly IUserInfo _userInfo;

    public Lobby(PlayerProfileService playerProfiles,
        ProfileService profiles,
        GameSetupService gameSetup,
        IUserInfo userInfo)
    {
        _playerProfiles = playerProfiles;
        _profiles = profiles;
        _gameSetup = gameSetup;
        _userInfo = userInfo;
    }

    public string GameId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string GameName { get; private set; } = string.Empty;
    public UserProfileViewModel? Profile { get; private set; }
    public ushort? Dice1 { get; private set; }
    public ushort? Dice2 { get; private set; }

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public AlertType StatusAlertType => StatusKind == "danger" ? AlertType.Error : AlertType.Success;

    public async Task<IActionResult> OnGetAsync(string gameId, string userId)
    {
        //Lobby page for player A is only visible to logged in user A.
        //Index page allows any user (in the game) to view, allowing host "tablet/controller" to view.
        if(userId != _userInfo.UserId)
            return Forbid();
        
        var player = await _playerProfiles.GetPlayerForGameSetup(gameId, userId);
        if (player is null)
        {
            player = await _playerProfiles.GetPlayerForGamePlay(gameId, userId);
            if (player is null) return NotFound();
            
            return RedirectToPage("/Index", new { gameId, userId });
        }

        Profile = await _profiles.GetProfileViewModelAsync(userId);
        if (Profile is null) return NotFound();

        GameId = gameId;
        UserId = userId;
        GameName = player.Game.Name;
        Dice1 = player.Dice1;
        Dice2 = player.Dice2;
        return Page();
    }

    public async Task<IActionResult> OnPostSetDiceAsync(string gameId, string userId, ushort dice1, ushort dice2)
    {
        if (_userInfo.UserId != userId) return Forbid();

        var ok = await _gameSetup.TrySetPlayerDiceNumbers(gameId, userId, dice1, dice2);
        StatusMessage = ok ? "Dice numbers set." : "Could not set those dice numbers — they may already be taken.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { gameId, userId });
    }

    public async Task<IActionResult> OnPostLeaveAsync(string gameId, string userId)
    {
        if (_userInfo.UserId != userId) return Forbid();

        var result = await _gameSetup.TryLeaveGame(gameId, userId);
        if (result) return RedirectToPage("/Index", new { area = "" });
        
        StatusMessage = "Could not leave the game.";
        StatusKind = "danger";
        return RedirectToPage(new { gameId, userId });

    }
}