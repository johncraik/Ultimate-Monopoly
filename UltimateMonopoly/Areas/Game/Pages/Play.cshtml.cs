using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Models;
using MP.GameEngine.Services.Framework;

namespace UltimateMonopoly.Areas.Game.Pages;

/// <summary>
/// Host / tablet in-game view (<c>/Game/Play/{gameId}</c>). First load pulls the
/// whole game state straight from the engine cache; live updates will arrive over
/// SignalR (the game-play hub) once that layer is wired — see the page's TODO.
/// </summary>
public class PlayModel : PageModel
{
    private readonly IGameEngineFactory _engineFactory;

    public PlayModel(IGameEngineFactory engineFactory)
    {
        _engineFactory = engineFactory;
    }

    /// <summary>The live game cache — the single source the view renders from.</summary>
    public GameEngine Engine { get; private set; }

    public async Task<IActionResult> OnGetAsync(string gameId)
    {
        // TODO(auth): gate to game members (and surface host-only controls to the
        // host). Group membership is already enforced on the game-play hub and
        // commands authorise per-action (host-bypass), so this is the page-level
        // gate only.
        try
        {
            Engine = await _engineFactory.GetAsync(gameId);
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
            Engine = await _engineFactory.GetAsync(gameId);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        return Partial("Play/_PlayView", Engine);
    }
}