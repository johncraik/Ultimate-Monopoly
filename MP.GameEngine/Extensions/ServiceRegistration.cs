using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Services;
using MP.GameEngine.Services.Framework;

namespace MP.GameEngine.Extensions;

public static class ServiceRegistration
{
    public static IServiceCollection AddGameEngine(this IServiceCollection services)
    {
        services.TryAddScoped<IPromptProvider, PromptProvider>();
        
        services.TryAddScoped<GameSetupService>();
        services.TryAddScoped<PlayerService>();
        services.TryAddScoped<PropertyService>();
        
        return services;
    }
}