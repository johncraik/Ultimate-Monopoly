using JC.Core.Models.Pagination;
using UltimateMonopoly.Areas.Admin.Enums;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>Backs the reusable <c>_AdminLogsTable</c> partial: a page of admin-action log entries plus the
/// current search / action / target-type filters (so the pagination links and the no-JS fallback carry them).</summary>
public class AdminLogTableModel
{
    public PagedList<AdminLogViewModel> Logs { get; }
    public string? Search { get; }
    public AdminActionType? Action { get; }
    public AdminTargetType? TargetType { get; }

    public AdminLogTableModel(PagedList<AdminLogViewModel> logs, string? search,
        AdminActionType? action, AdminTargetType? targetType)
    {
        Logs = logs;
        Search = search;
        Action = action;
        TargetType = targetType;
    }
}
