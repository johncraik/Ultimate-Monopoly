using JC.Core.Models.Pagination;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>Backs the <c>_EmailLogsTable</c> partial: a page of email logs plus the current search
/// (so the pagination links and the no-JS fallback carry it).</summary>
public class EmailLogTableModel
{
    public PagedList<EmailLogViewModel> Logs { get; }
    public string? Search { get; }

    public EmailLogTableModel(PagedList<EmailLogViewModel> logs, string? search)
    {
        Logs = logs;
        Search = search;
    }
}
