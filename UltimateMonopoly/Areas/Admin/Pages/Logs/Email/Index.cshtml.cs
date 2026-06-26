using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs.Email;

/// <summary>The outbound email log viewer (C1 §10). Search-only; rows expand to the email's recipients and
/// send attempts. Metadata only — no body is ever shown (prod logs ExcludeContent).</summary>
public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly AppLogService _logs;

    public IndexModel(AppLogService logs) => _logs = logs;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<EmailLogViewModel> Logs { get; set; } = new([], 1, PageSize, 0);

    public EmailLogTableModel TableModel => new(Logs, Search);

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — just the table partial for the current search/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Logs/Email/_EmailLogsTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Logs = await _logs.GetEmailLogs(PageNumber, PageSize, Search);
    }
}
