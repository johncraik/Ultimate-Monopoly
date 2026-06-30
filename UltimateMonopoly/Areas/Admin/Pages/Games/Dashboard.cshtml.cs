using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Areas.Admin.Services.Dashboard;

namespace UltimateMonopoly.Areas.Admin.Pages.Games;

/// <summary>The Games spoke dashboard (C1 — lifecycle, players/length, board/config, storage, activity).
/// <c>SystemAdminOnly</c> — the policy blocks non-SysAdmins before model construction, so the service's
/// ctor guard never throws on this page.</summary>
[Authorize(Policy = "SystemAdminOnly")]
public class DashboardModel : PageModel
{
    private readonly GamesDashboardService _dashboard;

    public DashboardModel(GamesDashboardService dashboard) => _dashboard = dashboard;

    public GamesDashboardModel Data { get; private set; } = default!;

    public async Task OnGetAsync() => Data = await _dashboard.Build();
}
