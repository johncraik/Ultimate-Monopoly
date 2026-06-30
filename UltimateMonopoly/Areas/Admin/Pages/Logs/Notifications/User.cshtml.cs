using JC.Communication.Notifications.Models;
using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs.Notifications;

/// <summary>A user's notifications (option A). The user id is a ROUTE param. Filters: search + type +
/// read-state + status (active / dismissed / expired — each a distinct NotificationService call). Each row
/// expands to its read/unread log history.</summary>
public class UserModel : PageModel
{
    private const int PageSize = 30;

    private readonly AppLogService _logs;
    private readonly UserManagementService _users;

    public UserModel(AppLogService logs, UserManagementService users)
    {
        _logs = logs;
        _users = users;
    }

    public string UserId { get; private set; } = "";
    public UserViewModel? Recipient { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public NotificationType? Type { get; set; }

    // null = All, true = Read, false = Unread.
    [BindProperty(SupportsGet = true)]
    public bool? Read { get; set; }

    [BindProperty(SupportsGet = true)]
    public NotificationStatusFilter Status { get; set; } = NotificationStatusFilter.Active;

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<NotificationViewModel> Notifications { get; set; } = new([], 1, PageSize, 0);

    public NotificationTableModel TableModel => new(Notifications, UserId, Search, Type, Read, Status);

    public async Task OnGetAsync(string userId)
    {
        UserId = userId;
        Recipient = await _users.GetUserById(userId);
        await LoadAsync();
    }

    /// <summary>AJAX endpoint — just the table partial for the current filter/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync(string userId)
    {
        UserId = userId;
        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Logs/Notifications/_NotificationsTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Notifications = await _logs.GetUserNotifications(UserId, PageNumber, PageSize, Search, Type, Read, Status);
    }
}
