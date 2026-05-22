using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Services.Cache;

namespace UltimateMonopoly.Pages;

public class IndexModel : PageModel
{
    private readonly BoardCacheService _boardCacheService;

    public IndexModel(BoardCacheService boardCacheService)
    {
        _boardCacheService = boardCacheService;
    }
    
    public async Task<IActionResult> OnGet(bool? bypass = false)
    {
        if (bypass == false)
            return RedirectToPage("/Games/MyGames");
        
        var defaultBoard = await _boardCacheService.GetDefaultBoard();
        var all = await _boardCacheService.GetAllBoards();
        return Page();
    }
}
