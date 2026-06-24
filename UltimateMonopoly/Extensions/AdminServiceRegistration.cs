using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimateMonopoly.Areas.Admin.Middleware;
using UltimateMonopoly.Areas.Admin.Services;

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

        // Singleton registry of users flagged for a live-session sign-in refresh (role/account changes).
        services.TryAddSingleton<AuthRefreshService>();

        return services;
    }
}