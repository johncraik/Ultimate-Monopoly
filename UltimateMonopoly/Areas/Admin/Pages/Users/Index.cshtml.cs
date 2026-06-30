using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Users;

public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly UserManagementService _users;

    public IndexModel(UserManagementService users) => _users = users;

    // The two filter axes. Each binds one of the UserManagementFilter flags (the radios offer
    // only the values for that axis); OR'd together they form the combined filter the service takes.
    // The sidebar's "Restricted Users" / "Disabled Users" links preset the matching one.
    [BindProperty(SupportsGet = true)]
    public UserManagementFilter RestrictedFilter { get; set; } = UserManagementFilter.None;

    [BindProperty(SupportsGet = true)]
    public UserManagementFilter DisabledFilter { get; set; } = UserManagementFilter.None;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // NB: must NOT be named "page" — that's a reserved Razor Pages route key (the page path), and the
    // route value provider wins over the query string, so a "page" query param never binds. Use "pageNumber".
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<UserViewModel> Users { get; set; } = new ([], 1, 30, 0);

    public UserTableModel TableModel => new (Users, Search, RestrictedFilter, DisabledFilter);
    
    
    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — returns just the table partial for the filter/search/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("_UsersTable", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Users = await _users.GetUsers(PageNumber, PageSize, Search, RestrictedFilter | DisabledFilter);
    }
}
