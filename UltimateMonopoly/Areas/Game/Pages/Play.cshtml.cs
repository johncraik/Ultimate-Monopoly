using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Services.Framework;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Areas.Game.Pages;

public class PlayModel : PageModel
{
    private readonly IGameEngineFactory _engineFactory;
    private readonly IUserInfo _userInfo;
    private readonly GameService _gameService;
    private readonly IEngineNotifier _notifier;

    public PlayModel(IGameEngineFactory engineFactory,
        IUserInfo userInfo,
        GameService gameService,
        IEngineNotifier notifier)
    {
        _engineFactory = engineFactory;
        _userInfo = userInfo;
        _gameService = gameService;
        _notifier = notifier;
    }

    /// <summary>The live game cache — the single source the view renders from.</summary>
    public GameEngine Engine { get; private set; }

    public async Task<IActionResult> OnGetAsync(string gameId)
    {
        try
        {
            var redirect = await LoadAsync(gameId);
            if (redirect is not null) return redirect;
        }
        catch (InvalidOperationException)
        {
            // No active cache — game not started yet, or its snapshot could not
            // be hydrated. (GameEngineFactory.GetAsync throws in that case.)
            return NotFound();
        }

        return Page();
    }

    /// <summary>
    /// Re-renders the live play body (the <c>_PlayView</c> partial) from the
    /// current cache. The page polls this on each SignalR StateChanged frame
    /// (play-state.js) and swaps it into <c>.play-page</c>, so the live render
    /// path is identical to first load — no client-side state duplication.
    /// </summary>
    public async Task<IActionResult> OnGetStateAsync(string gameId)
    {
        try
        {
            var redirect = await LoadAsync(gameId);
            if (redirect is not null) return redirect;
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        return Partial("Play/_PlayView", Engine);
    }

    /// <summary>
    /// Host "Cancel Game" (the host panel's confirm-POST). Cancels the game — no winner/draw —
    /// which also tears down the live runtime (cache + pump), then broadcasts so the other
    /// clients (players' phones) redirect home; the host follows the POST redirect itself.
    /// <see cref="GameService.TryCancelGame"/> is scoped to the game's creator, so a non-host
    /// POST simply no-ops.
    /// </summary>
    public async Task<IActionResult> OnPostCancelAsync(string gameId)
    {
        var cancelled = await _gameService.TryCancelGame(gameId);
        if (cancelled)
            _notifier.GameCancelled(gameId);

        return Redirect("/Index");
    }

    private async Task<IActionResult?> LoadAsync(string gameId)
    {
    Engine = await _engineFactory.GetAsync(gameId);
        if(Engine.Cache.HostPlayerId != _userInfo.UserId)
            return Forbid();
            
        return Engine.Cache.GameState != GameState.InPlay ? NotFound() : null;
    }
}