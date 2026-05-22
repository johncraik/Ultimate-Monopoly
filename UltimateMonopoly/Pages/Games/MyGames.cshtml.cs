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

    public IActionResult OnPostDelete(string gameId)
    {
        // TODO: soft-delete the game — backend not yet implemented.
        return RedirectToPage();
    }
}