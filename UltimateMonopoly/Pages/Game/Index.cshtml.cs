using JC.Web.UI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using UltimateMonopoly.Enums.Games;
using UltimateMonopoly.Services.BoardSkins;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Pages.Game;

[Authorize]
public class IndexModel : PageModel
{
    private readonly GameSetupService _gameSetup;
    private readonly BoardSkinService _boardSkins;

    public IndexModel(GameSetupService gameSetup, BoardSkinService boardSkins)
    {
        _gameSetup = gameSetup;
        _boardSkins = boardSkins;
    }

    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? BoardSkinId { get; set; }
    [BindProperty] public GameRoundingRule RoundingRule { get; set; } = GameRoundingRule.To50;

    public List<SelectListItem> Boards { get; private set; } = [];

    public async Task OnGetAsync() => await LoadBoardsAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var modelState = new ModelStateWrapper(ModelState, ignorePrefix: true);
        var boardSkinId = string.IsNullOrEmpty(BoardSkinId) ? null : BoardSkinId;

        var result = await _gameSetup.TryCreateNewGame(modelState, Name, boardSkinId, RoundingRule);
        if (result.Result)
            return RedirectToPage("./Setup", new { id = result.GameId });

        if (ModelState.IsValid)
            ModelState.AddModelError(string.Empty, "Could not create the game. Please try again.");

        await LoadBoardsAsync();
        return Page();
    }

    private async Task LoadBoardsAsync()
    {
        Boards = await _boardSkins.GetBoardDropdown();
        Boards.Insert(0, new SelectListItem("Default board", string.Empty));
    }
}