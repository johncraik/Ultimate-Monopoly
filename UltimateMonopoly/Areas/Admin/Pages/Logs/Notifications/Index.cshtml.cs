using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs.Notifications;

/// <summary>Notification-logs user picker — a searchable user list (reuses <c>_UsersTable</c> in its audit
/// variant), each row opening that user's notifications. A top-right button jumps to the global read/unread
/// log. Mirrors the User-Trail picker.</summary>
public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly UserManagementService _users;

    public IndexModel(UserManagementService users) => _users = users;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<UserViewModel> Users { get; set; } = new([], 1, PageSize, 0);

    // RowLinkBase points rows at the per-user notifications page instead of the audit trail.
    public UserTableModel TableModel => new(Users, Search, rowLinkBase: "/Admin/Logs/Notifications/User");

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — just the table partial for the current search/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Users/_UsersTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Users = await _users.GetUsers(PageNumber, PageSize, Search, UserManagementFilter.None);
    }
}
