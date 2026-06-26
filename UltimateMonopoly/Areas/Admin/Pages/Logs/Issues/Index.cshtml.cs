using JC.Core.Models.Pagination;
using JC.Github.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs.Issues;

/// <summary>The reported-issues viewer (C1 — JC.Github). Read-only: bugs / suggestions reported in-app or
/// synced from GitHub, with type / status filters and search; rows expand to the full description, any
/// screenshot, and the issue's GitHub comments.</summary>
public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly AppLogService _logs;

    public IndexModel(AppLogService logs) => _logs = logs;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public IssueType? Type { get; set; }

    // null = All, false = Open, true = Closed.
    [BindProperty(SupportsGet = true)]
    public bool? Closed { get; set; }

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<ReportedIssueViewModel> Issues { get; set; } = new([], 1, PageSize, 0);

    public ReportedIssueTableModel TableModel => new(Issues, Search, Type, Closed);

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — just the table partial for the current filter/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Logs/Issues/_ReportedIssuesTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Issues = await _logs.GetReportedIssues(PageNumber, PageSize, Search, Type, Closed);
    }
}
