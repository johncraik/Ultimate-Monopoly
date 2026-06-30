using JC.Communication.Logging.Models.Messaging;
using JC.Core.Models.Pagination;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>Backs the <c>_ThreadActivityLogsTable</c> partial: a page of thread-activity events plus the
/// current search / activity-type filter (so the pagination links and the no-JS fallback carry them).</summary>
public class ThreadActivityLogTableModel
{
    public PagedList<ThreadActivityLogViewModel> Logs { get; }
    public string? Search { get; }
    public ThreadActivityType? ActivityType { get; }

    /// <summary>Preview mode (the §7.3 Recent Activity panel): the partial drops its pagination + count header.</summary>
    public bool Preview { get; init; }

    public ThreadActivityLogTableModel(PagedList<ThreadActivityLogViewModel> logs, string? search, ThreadActivityType? activityType)
    {
        Logs = logs;
        Search = search;
        ActivityType = activityType;
    }
}
