using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Models.ViewModels.Games;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Pages.Games;

public class MyGamesModel : PageModel
{
    private readonly GameService _game;

    public MyGamesModel(GameService game)
    {
        _game = game;
    }

    public List<GameViewModel> Games { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Games = await _game.GetAllMyGames();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string gameId)
    {
        // Soft-deletes the game (+ its players/turns/snapshots/events). Scoped to the
        // creator's cancelled games, so a non-owner / non-cancelled post simply no-ops.
        await _game.TryDeleteGame(gameId);
        return RedirectToPage();
    }
}