using JC.Core.Enums;
using JC.Core.Models.Pagination;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;

/// <summary>Backs the reusable <c>_AuditTable</c> partial: a page of audit entries plus the current
/// search / action filter (so the pagination links and the no-JS fallback carry them).</summary>
public class AuditTableModel
{
    public PagedList<AuditEntryViewModel> Entries { get; }
    public string? Search { get; }
    public AuditAction? Action { get; }

    public AuditTableModel(PagedList<AuditEntryViewModel> entries, string? search, AuditAction? action)
    {
        Entries = entries;
        Search = search;
        Action = action;
    }
}