using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs.Admin;

/// <summary>The admin-action log viewer (C1 §10). Search-only over the <c>AdminActionLog</c> trail; rows
/// expand to show the full detail. Mirrors the audit-trail pages (AJAX table partial + no-JS fallback).</summary>
public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly AppLogService _logs;

    public IndexModel(AppLogService logs) => _logs = logs;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // Filters — null (the "All" option) means no filter.
    [BindProperty(SupportsGet = true)]
    public AdminActionType? Action { get; set; }

    [BindProperty(SupportsGet = true)]
    public AdminTargetType? TargetType { get; set; }

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<AdminLogViewModel> Logs { get; set; } = new([], 1, PageSize, 0);

    public AdminLogTableModel TableModel => new(Logs, Search, Action, TargetType);

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — just the table partial for the current search/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Logs/_AdminLogsTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Logs = await _logs.GetAdminLogs(PageNumber, PageSize, Search, Action, TargetType);
    }
}
