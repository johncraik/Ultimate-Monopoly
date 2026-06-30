using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Areas.Admin.Services.Dashboard;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs;

/// <summary>The App Logs spoke dashboard (C1 — admin-action log + issues + comms). Admin+.</summary>
public class DashboardModel : PageModel
{
    private readonly AppLogsDashboardService _dashboard;

    public DashboardModel(AppLogsDashboardService dashboard) => _dashboard = dashboard;

    public AppLogsDashboardModel Data { get; private set; } = default!;

    public async Task OnGetAsync() => Data = await _dashboard.Build();
}
