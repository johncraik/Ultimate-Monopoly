using JC.Core.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimateMonopoly.Models;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.GameConfig;
using UltimateMonopoly.Services.Imports;

namespace UltimateMonopoly.Extensions;

public static class ServiceRegistration
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.RegisterRepositoryContexts(
            typeof(CustomBoard), 
            typeof(CustomBoardSpace));
        
        services.TryAddSingleton<FilePathProvider>();
        
        services.TryAddScoped<BoardImportService>();
        services.TryAddScoped<BoardCacheService>();

        return services;
    }
}