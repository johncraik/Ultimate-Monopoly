using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Models.ViewModels.Games;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Pages.Games;

public class JoinedModel : PageModel
{
    private readonly GameService _game;

    public JoinedModel(GameService game)
    {
        _game = game;
    }

    public List<GameViewModel> Games { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Games = await _game.GetAllGamesJoined();
    }
}