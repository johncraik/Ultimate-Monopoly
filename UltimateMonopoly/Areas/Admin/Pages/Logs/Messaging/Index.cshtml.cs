using JC.Communication.Logging.Models.Messaging;
using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs.Messaging;

/// <summary>The messaging thread-activity log viewer (C1 §10) — message sends and participant add/remove
/// events. Search + activity-type filter; rows expand to the full detail. Read receipts (MessageReadLog)
/// are intentionally not shown — they back the chat UI's "read @ {time}" stamps, not admin moderation.</summary>
public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly AppLogService _logs;

    public IndexModel(AppLogService logs) => _logs = logs;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public ThreadActivityType? ActivityType { get; set; }

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<ThreadActivityLogViewModel> Logs { get; set; } = new([], 1, PageSize, 0);

    public ThreadActivityLogTableModel TableModel => new(Logs, Search, ActivityType);

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — just the table partial for the current search/type/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Logs/Messaging/_ThreadActivityLogsTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Logs = await _logs.GetThreadActivityLogs(PageNumber, PageSize, Search, ActivityType);
    }
}
