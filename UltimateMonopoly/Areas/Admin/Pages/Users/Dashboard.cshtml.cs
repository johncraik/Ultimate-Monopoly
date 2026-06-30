using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Areas.Admin.Services.Dashboard;

namespace UltimateMonopoly.Areas.Admin.Pages.Users;

/// <summary>The Users spoke dashboard (C1 — identity health, activity, account safety). Admin+; composed
/// from the reusable dashboard widgets via <see cref="UserDashboardService"/>.</summary>
public class DashboardModel : PageModel
{
    private readonly UserDashboardService _dashboard;

    public DashboardModel(UserDashboardService dashboard) => _dashboard = dashboard;

    public UserDashboardModel Data { get; private set; } = default!;

    public async Task OnGetAsync() => Data = await _dashboard.Build();
}
