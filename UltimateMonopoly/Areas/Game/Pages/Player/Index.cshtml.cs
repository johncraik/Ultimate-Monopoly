using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Services.Framework;
using UltimateMonopoly.Models.ViewModels.Games;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Areas.Game.Pages.Player;

/// <summary>
/// Mobile player profile (<c>/player-profile/{gameId}/{userId}</c>). Like the host
/// <see cref="PlayModel"/>, the body (<c>_PlayerProfileView</c>) renders straight
/// off the engine cache; live updates re-fetch it via <see cref="OnGetStateAsync"/>
/// (handler=State) on each SignalR StateChanged frame and swap it in, so the live
/// and first-load render paths are identical. The engine prompts (dice, acquire, …)
/// surface as separate modals driven by the game-play hub coordinator.
/// </summary>
public class Index : PageModel
{
    private readonly PlayerProfileService _playerProfiles;
    private readonly ProfileService _profiles;
    private readonly GameService _gameService;
    private readonly IGameEngineFactory _engineFactory;
    private readonly IUserInfo _userInfo;

    public Index(PlayerProfileService playerProfiles,
        ProfileService profiles,
        GameService gameService,
        IGameEngineFactory engineFactory,
        IUserInfo userInfo)
    {
        _playerProfiles = playerProfiles;
        _profiles = profiles;
        _gameService = gameService;
        _engineFactory = engineFactory;
        _userInfo = userInfo;
    }

    public string GameId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public UserProfileViewModel? Profile { get; private set; }

    /// <summary>The live engine bundle — the single source the view renders from.</summary>
    public GameEngine Engine { get; private set; } = null!;

    /// <summary>Model passed to <c>_PlayerProfileView</c> on both first load and the State re-fetch.</summary>
    public PlayerProfilePlayViewModel ProfileView { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string gameId, string userId)
    {
        var result = await LoadAsync(gameId, userId);
        if (result is not null) return result;

        // Title only — the body renders from the engine cache via the partial.
        Profile = await _profiles.GetProfileViewModelAsync(userId);
        if (Profile is null) return NotFound();

        return Page();
    }

    /// <summary>
    /// Re-renders the live profile body (<c>_PlayerProfileView</c>) from the
    /// current cache. The page re-fetches this on each SignalR StateChanged frame
    /// (player-state.js) and swaps it in, mirroring the host play page.
    /// </summary>
    public async Task<IActionResult> OnGetStateAsync(string gameId, string userId)
    {
        var result = await LoadAsync(gameId, userId);
        if (result is not null) return result;

        return Partial("Play/_PlayerProfileView", ProfileView);
    }

    /// <summary>
    /// Shared load + auth for both handlers. Any player in the game may view the
    /// page (the host views a player's profile from the tablet); per-action
    /// authority is enforced by the engine's capability gates / prompt validator,
    /// keyed off <see cref="ProfileView"/>'s viewer id. Returns a non-null result
    /// to short-circuit, or null on success.
    /// </summary>
    private async Task<IActionResult?> LoadAsync(string gameId, string userId)
    {
        if (!await _gameService.CheckUserInGame(gameId, _userInfo.UserId))
            return Forbid();

        var player = await _playerProfiles.GetPlayerForGamePlay(gameId, userId);
        if (player is null) return NotFound();

        try
        {
            Engine = await _engineFactory.GetAsync(gameId);
        }
        catch (InvalidOperationException)
        {
            // No active cache — game not started, or its snapshot couldn't hydrate.
            return NotFound();
        }

        if (Engine.Cache.GameState != GameState.InPlay) return NotFound();

        GameId = gameId;
        UserId = userId;
        ProfileView = new PlayerProfilePlayViewModel(Engine, userId, _userInfo.UserId);
        return null;
    }
}