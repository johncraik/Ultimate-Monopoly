using JC.Core.Models.Pagination;
using JC.Github.Models;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>Backs the <c>_ReportedIssuesTable</c> partial: a page of reported issues plus the current
/// search / type / status filters (so the pagination links and the no-JS fallback carry them).</summary>
public class ReportedIssueTableModel
{
    public PagedList<ReportedIssueViewModel> Issues { get; }
    public string? Search { get; }
    public IssueType? Type { get; }

    /// <summary>null = All, false = Open, true = Closed.</summary>
    public bool? Closed { get; }

    public ReportedIssueTableModel(PagedList<ReportedIssueViewModel> issues, string? search, IssueType? type, bool? closed)
    {
        Issues = issues;
        Search = search;
        Type = type;
        Closed = closed;
    }
}
