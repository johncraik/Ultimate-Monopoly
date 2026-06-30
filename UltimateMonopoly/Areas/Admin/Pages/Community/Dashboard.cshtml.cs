using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Areas.Admin.Services.Dashboard;

namespace UltimateMonopoly.Areas.Admin.Pages.Community;

/// <summary>The Community spoke dashboard (C1 — social graph + moderation). Admin+; reached only via the hub
/// tile (no nav entry — Community has no admin-area section). Composed from the reusable dashboard widgets.</summary>
public class DashboardModel : PageModel
{
    private readonly CommunityDashboardService _dashboard;

    public DashboardModel(CommunityDashboardService dashboard) => _dashboard = dashboard;

    public CommunityDashboardModel Data { get; private set; } = default!;

    public async Task OnGetAsync() => Data = await _dashboard.Build();
}
