using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Pages.Game;

public class Join : PageModel
{
    private readonly GameSetupService _gameSetupService;
    private readonly IUserInfo _userInfo;

    public Join(GameSetupService gameSetupService,
        IUserInfo userInfo)
    {
        _gameSetupService = gameSetupService;
        _userInfo = userInfo;
    }

    public string? ErrorMessage { get; private set; }
    public bool ShowCodeEntry { get; private set; }

    [BindProperty] public string? Code { get; set; }

    public async Task<IActionResult> OnGet(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            ShowCodeEntry = true;
            return Page();
        }

        var joinResult = await _gameSetupService.TryJoinGame(id, _userInfo.UserId);
        if (joinResult.Result)
            return RedirectToPage("/Game/Waiting", new { id });

        ErrorMessage = joinResult.Message ?? "Unable to join this game.";
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        ShowCodeEntry = true;

        var code = Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            ErrorMessage = "Enter a join code.";
            return Page();
        }

        var joinResult = await _gameSetupService.TryJonGameFromCode(code, _userInfo.UserId);
        if (joinResult.Result)
            return RedirectToPage("/Game/Waiting", new { id = joinResult.GameId });

        ErrorMessage = joinResult.Message ?? "Unable to join this game.";
        return Page();
    }
}