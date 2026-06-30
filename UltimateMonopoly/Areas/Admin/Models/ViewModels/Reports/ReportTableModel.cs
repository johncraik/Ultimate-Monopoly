using JC.Core.Models.Pagination;
using UltimateMonopoly.Areas.Admin.Enums;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Reports;

public class ReportTableModel
{
    public ReportResolution Resolution { get; }
    public string? Search { get; }
    public PagedList<ReportViewModel> Reports { get; }

    public ReportTableModel(PagedList<ReportViewModel> reports, string? search, ReportResolution resolution)
    {
        Reports = reports;
        Search = search;
        Resolution = resolution;
    }
}