using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Services.GameConfig;

namespace UltimateMonopoly.Pages;

public class IndexModel : PageModel
{
    private readonly BoardCacheService _boardCacheService;

    public IndexModel(BoardCacheService boardCacheService)
    {
        _boardCacheService = boardCacheService;
    }
    
    public async Task OnGet()
    {
        var defaultBoard = await _boardCacheService.GetDefaultBoard();
        var all = await _boardCacheService.GetAllBoards();
    }
}
