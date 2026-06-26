using JC.Core.Enums;
using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Audit.Users;

/// <summary>One user's audit trail (§9.3). The user id is a ROUTE param (<c>@@page "{userId?}"</c>), not a
/// bind property or query string. Search filters by table name; the Action radios filter by audit action.
/// With no userId and <c>?system=true</c>, the same page shows the System / background-actor trail.</summary>
public class TrailModel : PageModel
{
    private const int PageSize = 30;

    private readonly AuditTrailService _audit;
    private readonly UserManagementService _users;

    public TrailModel(AuditTrailService audit, UserManagementService users)
    {
        _audit = audit;
        _users = users;
    }

    public string? UserId { get; private set; }
    public UserViewModel? User { get; private set; }

    // No userId + ?system=true → the System / background-actor trail (same page).
    public bool IsSystem { get; private set; }

    [BindProperty(SupportsGet = true)]
    public bool System { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public AuditAction? Action { get; set; }

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<AuditEntryViewModel> Entries { get; set; } = new([], 1, PageSize, 0);

    // IsUserTrail = true → the shared _AuditTable shows the Table column (the user is fixed here).
    public AuditTableModel TableModel => new(Entries, Search, Action, true);

    public async Task<IActionResult> OnGetAsync(string? userId)
    {
        UserId = userId;
        IsSystem = string.IsNullOrEmpty(userId) && System;
        // Neither a user nor the system trail — nothing to show.
        if (string.IsNullOrEmpty(userId) && !IsSystem) return NotFound();

        if (!IsSystem && !string.IsNullOrEmpty(userId))
            User = await _users.GetUserById(userId);
        await LoadAsync();
        return Page();
    }

    /// <summary>AJAX endpoint — just the table partial for the current search/action/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync(string? userId)
    {
        UserId = userId;
        IsSystem = string.IsNullOrEmpty(userId) && System;
        if (string.IsNullOrEmpty(userId) && !IsSystem) return NotFound();

        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Audit/_AuditTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Entries = IsSystem
            ? await _audit.GetSystemTrail(PageNumber, PageSize, Search, Action)
            : await _audit.GetUserTrail(UserId ?? "", PageNumber, PageSize, Search, Action);
    }
}