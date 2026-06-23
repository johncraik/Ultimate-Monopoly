using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Extensions;

public static class AdminServiceRegistration
{
    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.TryAddScoped<AdminLogService>();
        services.TryAddScoped<UserManagementService>();
        
        return services;
    }
}