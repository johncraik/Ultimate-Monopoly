using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs.Notifications;

/// <summary>The global notification read/unread event log (every <c>NotificationLog</c> entry). Search +
/// read-state filter; rows render via the shared <c>_NotificationLogsTable</c> partial.</summary>
public class LogModel : PageModel
{
    private const int PageSize = 30;

    private readonly AppLogService _logs;

    public LogModel(AppLogService logs) => _logs = logs;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // null = All, true = Read events, false = Unread events.
    [BindProperty(SupportsGet = true)]
    public bool? Read { get; set; }

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<NotificationLogViewModel> Logs { get; set; } = new([], 1, PageSize, 0);

    public NotificationLogTableModel TableModel => new(Logs, Search, Read);

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — just the table partial for the current search/read/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Logs/Notifications/_NotificationLogsTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Logs = await _logs.GetNotificationLogs(PageNumber, PageSize, Search, Read);
    }
}
