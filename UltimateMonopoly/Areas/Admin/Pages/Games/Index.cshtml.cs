using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Games;

[Authorize(Policy = "SystemAdminOnly")]
public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly GameManagementService _games;

    public IndexModel(GameManagementService games) => _games = games;

    // Two filter axes — empty (the "All" radio) binds to null = no filter.
    [BindProperty(SupportsGet = true)]
    public GameState? State { get; set; }

    [BindProperty(SupportsGet = true)]
    public GameOutcome? Outcome { get; set; }

    // Normal search (name / join code / player ids) and the exact host-id search.
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? HostId { get; set; }

    // NB: "pageNumber", never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<GameViewModel> Games { get; set; } = new([], 1, PageSize, 0);

    public GameTableModel TableModel => new(Games, Search, HostId, State, Outcome);


    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — returns just the table partial for the filter/search/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("_GamesTable", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Games = await _games.GetGames(PageNumber, PageSize, Search, HostId, State, Outcome);
    }
}