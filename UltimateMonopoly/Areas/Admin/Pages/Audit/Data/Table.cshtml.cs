using JC.Core.Enums;
using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Audit.Data;

/// <summary>One table's audit trail (§9.2). The table name is a ROUTE param (<c>@@page "{tableName}"</c>),
/// not a bind property or query string — mirrors the User <c>Trail</c> page. Search filters within the
/// table (user / entity key, per the service); the Action radios filter by audit action.</summary>
public class DataTableModel : PageModel
{
    private const int PageSize = 30;

    private readonly AuditTrailService _audit;

    public DataTableModel(AuditTrailService audit) => _audit = audit;

    public string TableName { get; private set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public AuditAction? Action { get; set; }

    // Never "page" — reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<AuditEntryViewModel> Entries { get; set; } = new([], 1, PageSize, 0);

    // IsUserTrail = false → the shared _AuditTable shows the User column (the table is fixed here).
    public AuditTableModel TableModel => new(Entries, Search, Action, false);

    public async Task OnGetAsync(string tableName)
    {
        TableName = tableName;
        await LoadAsync();
    }

    /// <summary>AJAX endpoint — just the table partial for the current search/action/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync(string tableName)
    {
        TableName = tableName;
        await LoadAsync();
        return Partial("~/Areas/Admin/Pages/Audit/_AuditTable.cshtml", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Entries = await _audit.GetDataTableTrail(TableName, PageNumber, PageSize, Search, Action);
    }
}