using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Areas.Admin.Services.Dashboard;

namespace UltimateMonopoly.Areas.Admin.Pages.Audit;

/// <summary>The Audit spoke dashboard (C1 — JC.Core audit-trail trends). Admin+.</summary>
public class DashboardModel : PageModel
{
    private readonly AuditDashboardService _dashboard;

    public DashboardModel(AuditDashboardService dashboard) => _dashboard = dashboard;

    public AuditDashboardModel Data { get; private set; } = default!;

    public async Task OnGetAsync() => Data = await _dashboard.Build();
}
