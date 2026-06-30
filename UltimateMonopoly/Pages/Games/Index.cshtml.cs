using JC.Core.Models;
using JC.Web.UI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Data;
using UltimateMonopoly.Services.BoardSkins;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Pages.Games;

[Authorize]
public class IndexModel : PageModel
{
    private readonly GameSetupService _gameSetup;
    private readonly BoardSkinService _boardSkins;
    private readonly IUserInfo _userInfo;

    public IndexModel(GameSetupService gameSetup, 
        BoardSkinService boardSkins,
        IUserInfo userInfo)
    {
        _gameSetup = gameSetup;
        _boardSkins = boardSkins;
        _userInfo = userInfo;
    }

    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? BoardSkinId { get; set; }
    [BindProperty] public GameRoundingRule RoundingRule { get; set; } = GameRoundingRule.To50;

    public List<SelectListItem> Boards { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync() 
    {
        if(_userInfo.IsInRole(AppRoles.Restricted))
           return Unauthorized();
        
        await LoadBoardsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var modelState = new ModelStateWrapper(ModelState, ignorePrefix: true);
        var boardSkinId = string.IsNullOrEmpty(BoardSkinId) ? null : BoardSkinId;

        var result = await _gameSetup.TryCreateNewGame(modelState, Name, boardSkinId, RoundingRule);
        if (result.Result)
            return RedirectToPage("/Setup", new { area = "Game", id = result.GameId });

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