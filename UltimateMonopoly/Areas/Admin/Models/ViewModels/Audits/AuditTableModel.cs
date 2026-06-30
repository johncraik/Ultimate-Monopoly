using JC.Core.Enums;
using JC.Core.Models.Pagination;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;

/// <summary>Backs the reusable <c>_AuditTable</c> partial: a page of audit entries plus the current
/// search / action filter (so the pagination links and the no-JS fallback carry them).
/// <para><see cref="IsUserTrail"/> picks the partial's variable column (mirrors the
/// <c>UserTableModel.FullTable</c> convention): a <b>user</b> trail fixes the user, so it shows the
/// <b>Table</b> column; a <b>data</b> trail fixes the table, so it shows the <b>User</b> column. Carried
/// at the table level (not just per-entry) so the header is right even when the page is empty.</para></summary>
public class AuditTableModel
{
    public PagedList<AuditEntryViewModel> Entries { get; }
    public string? Search { get; }
    public AuditAction? Action { get; }
    public bool IsUserTrail { get; }

    /// <summary>Preview mode (the §7.3 Recent Activity panel): the partial drops its pagination + count header.</summary>
    public bool Preview { get; init; }

    public AuditTableModel(PagedList<AuditEntryViewModel> entries, string? search, AuditAction? action, bool isUserTrail)
    {
        Entries = entries;
        Search = search;
        Action = action;
        IsUserTrail = isUserTrail;
    }
}