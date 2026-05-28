using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Models;
using UltimateMonopoly.Services.Cache;

namespace UltimateMonopoly.Pages;

public class IndexModel : PageModel
{
    private readonly BoardCacheService _boardCacheService;
    private readonly GameCacheService _gameCacheService;
    private readonly IConfiguration _config;

    public IndexModel(BoardCacheService boardCacheService,
        GameCacheService gameCacheService,
        IConfiguration config)
    {
        _boardCacheService = boardCacheService;
        _gameCacheService = gameCacheService;
        _config = config;
    }
    
    public GameCacheModel? TestGame { get; private set; }
    
    public async Task<IActionResult> OnGet(bool? bypass = false)
    {
        if (bypass == false)
            return RedirectToPage("/Games/MyGames");
        
        var defaultBoard = await _boardCacheService.GetDefaultBoard();
        var all = await _boardCacheService.GetAllBoards();
        
        var testGame = await _gameCacheService.GetGame(_config["TestGameId"] ?? "");
        TestGame = testGame;
        return Page();
    }
}
