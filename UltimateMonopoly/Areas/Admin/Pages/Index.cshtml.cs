using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Dashboard;
using UltimateMonopoly.Areas.Admin.Services.Dashboard;

namespace UltimateMonopoly.Areas.Admin.Pages;

/// <summary>The admin dashboard hub — a tile per spoke (2–3 live KPIs + a "View →" link), tier-gated. Built
/// spokes render live; unbuilt ones show a "coming soon" placeholder so the layout is complete from day one.</summary>
public class IndexModel : PageModel
{
    private readonly IServiceProvider _services;
    private readonly IUserInfo _userInfo;

    public IndexModel(IServiceProvider services, IUserInfo userInfo)
    {
        _services = services;
        _userInfo = userInfo;
    }

    public bool IsSystemAdmin => _userInfo.IsInRole(SystemRoles.SystemAdmin);
    public SpokeTileModel? UsersTile { get; private set; }
    public SpokeTileModel? CommunityTile { get; private set; }
    public SpokeTileModel? GamesTile { get; private set; }
    public SpokeTileModel? AuditTile { get; private set; }
    public SpokeTileModel? AppLogsTile { get; private set; }

    public async Task OnGetAsync()
    {
        // The spoke services guard their ctor to Admin/SystemAdmin; resolve lazily (not ctor-injected) so a
        // GithubManager-only viewer — redirected to Issues by the page filter, but only *after* model
        // construction — never triggers the guard. OnGet runs only for Admin/SystemAdmin here.
        if (!_userInfo.IsInRole(SystemRoles.Admin) && !_userInfo.IsInRole(SystemRoles.SystemAdmin))
            return;

        var tile = await _services.GetRequiredService<UserDashboardService>().BuildTile();
        UsersTile = new SpokeTileModel
        {
            Title = "Users",
            Icon = "bi-people",
            Tone = "primary",
            Href = "/Admin/Users/Dashboard",
            Kpis = tile.Kpis,
            Alerts = tile.Alerts,
            Graphs = new[] { tile.Registrations }
        };

        var community = await _services.GetRequiredService<CommunityDashboardService>().BuildTile();
        CommunityTile = new SpokeTileModel
        {
            Title = "Community",
            Icon = "bi-people-fill",
            Tone = "info",
            Href = "/Admin/Community/Dashboard",
            Kpis = community.Kpis,
            Alerts = community.Alerts,
            Graphs = new[] { community.ReportsOverTime }
        };

        var audit = await _services.GetRequiredService<AuditDashboardService>().BuildTile();
        AuditTile = new SpokeTileModel
        {
            Title = "Audit",
            Icon = "bi-clipboard-data",
            Tone = "warning",
            Href = "/Admin/Audit/Dashboard",
            Kpis = audit.Kpis,
            Graphs = new[] { audit.EntriesOverTime }
        };

        var logs = await _services.GetRequiredService<AppLogsDashboardService>().BuildTile();
        AppLogsTile = new SpokeTileModel
        {
            Title = "App Logs",
            Icon = "bi-journal-text",
            Tone = "secondary",
            Href = "/Admin/Logs/Dashboard",
            Kpis = logs.Kpis,
            Alerts = logs.Alerts,
            Graphs = new[] { logs.AdminActionsOverTime }
        };

        // Games is SystemAdmin-only — build its tile (full width, two graphs) only for SystemAdmins.
        if (_userInfo.IsInRole(SystemRoles.SystemAdmin))
        {
            var games = await _services.GetRequiredService<GamesDashboardService>().BuildTile();
            GamesTile = new SpokeTileModel
            {
                Title = "Games",
                Icon = "bi-controller",
                Tone = "success",
                Href = "/Admin/Games/Dashboard",
                Kpis = games.Kpis,
                Alerts = games.Alerts,
                Graphs = new[] { games.GamesCreated, games.GamesConcluded }
            };
        }
    }
}
