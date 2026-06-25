using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Audit.Data;

/// <summary>
/// Data-trail landing (§9.2): every audited table with its entry count + latest audit date. Picking one
/// navigates to its full change history (<c>/Admin/Audit/Data/Table/{tableName}</c>). The set of distinct
/// tables is small and bounded, so this list isn't paginated or searched — unlike the per-table page.
/// </summary>
public class IndexModel : PageModel
{
    private readonly AuditTrailService _audit;

    public IndexModel(AuditTrailService audit) => _audit = audit;

    public List<AuditDataTableViewModel> Tables { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var tables = await _audit.GetAuditTableNames();
        Tables = tables.OrderBy(t => t.TableName, StringComparer.OrdinalIgnoreCase).ToList();
    }
}