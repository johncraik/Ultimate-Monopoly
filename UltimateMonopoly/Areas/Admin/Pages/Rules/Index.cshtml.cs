using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Models.ViewModels;

namespace UltimateMonopoly.Areas.Admin.Pages.Rules;

[Authorize(Policy = "SystemAdminOnly")]
public class IndexModel : PageModel
{
    private readonly RuleManagementService _rules;

    public IndexModel(RuleManagementService rules) => _rules = rules;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    private List<GameRule> Rules { get; set; } = [];

    public RuleTableModel TableModel => new(Rules, Search);


    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — returns just the grouped table partial for the current search.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("_RulesTable", TableModel);
    }

    // Shared by the page GET and the table-partial GET so both run the same (in-memory) search.
    private async Task LoadAsync() => Rules = await _rules.GetRules(Search);
}