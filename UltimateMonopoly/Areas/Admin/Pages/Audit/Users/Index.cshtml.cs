using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Audit.Users;

/// <summary>
/// User-trail picker: the list of users to choose from before viewing one's audit trail. Reuses the
/// User-Management table in its <b>audit variant</b> (<see cref="UserTableModel.FullTable"/> = false →
/// id / user / email + a trail link). Search only — no filters needed. The rows link to each user's
/// trail page (<c>/Admin/Audit/Users/Trail/{userId}</c>) via the shared partial.
/// </summary>
public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly UserManagementService _users;

    public IndexModel(UserManagementService users) => _users = users;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // Never "page" — that's a reserved Razor Pages route key (see Users/Index for the gotcha).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<UserViewModel> Users { get; set; } = new([], 1, PageSize, 0);

    // FullTable=false → the audit-users variant of the shared table.
    public UserTableModel TableModel => new(Users, Search);

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — returns just the table partial for the current search/page state.</summary>
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