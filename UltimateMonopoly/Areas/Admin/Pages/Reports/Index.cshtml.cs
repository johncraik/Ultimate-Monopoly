using JC.Core.Models.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Reports;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Reports;

public class IndexModel : PageModel
{
    private const int PageSize = 30;

    private readonly ReportManagementService _reports;

    public IndexModel(ReportManagementService reports) => _reports = reports;

    // Single filter axis — the resolution radio. The sidebar's "Open Reports" link presets Open.
    [BindProperty(SupportsGet = true)]
    public ReportResolution Resolution { get; set; } = ReportResolution.Open;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // NB: must be "pageNumber", never "page" — that's a reserved Razor Pages route key (see Users/Index).
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    private PagedList<ReportViewModel> Reports { get; set; } = new([], 1, PageSize, 0);

    public ReportTableModel TableModel => new(Reports, Search, Resolution);


    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>AJAX endpoint — returns just the table partial for the filter/search/page state.</summary>
    public async Task<IActionResult> OnGetTableAsync()
    {
        await LoadAsync();
        return Partial("_ReportsTable", TableModel);
    }

    private async Task LoadAsync()
    {
        if (PageNumber < 1) PageNumber = 1;
        Reports = await _reports.GetReports(PageNumber, PageSize, Search, Resolution);
    }
}