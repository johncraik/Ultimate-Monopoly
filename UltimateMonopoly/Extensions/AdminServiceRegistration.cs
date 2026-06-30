using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimateMonopoly.Areas.Admin.Middleware;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Areas.Admin.Services.Dashboard;

namespace UltimateMonopoly.Extensions;

public static class AdminServiceRegistration
{
    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.TryAddScoped<AdminLogService>();
        services.TryAddScoped<UserManagementService>();
        services.TryAddScoped<ReportManagementService>();
        services.TryAddScoped<RuleManagementService>();
        services.TryAddScoped<TurnTaxManagementService>();
        services.TryAddScoped<GameManagementService>();
        services.TryAddScoped<AdminGameStateService>();
        services.TryAddScoped<AuditTrailService>();
        services.TryAddScoped<AppLogService>();
        services.TryAddScoped<SettingsManagementService>();
        services.TryAddScoped<RecentActivityService>();
        services.TryAddScoped<IssueContactService>();

        // Dashboards (C1 — hub + per-spoke). Each builds reusable widget models for its spoke + the hub tile.
        services.TryAddScoped<UserDashboardService>();
        services.TryAddScoped<CommunityDashboardService>();
        services.TryAddScoped<GamesDashboardService>();
        services.TryAddScoped<AuditDashboardService>();
        services.TryAddScoped<AppLogsDashboardService>();

        // Singleton registry of users flagged for a live-session sign-in refresh (role/account changes).
        services.TryAddSingleton<AuthRefreshService>();

        // Singleton holder for the admin-configurable game settings (config/rules/settings.json).
        services.TryAddSingleton<SettingsDictionary>();

        return services;
    }
}