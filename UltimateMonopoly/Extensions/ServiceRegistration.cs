using JC.BackgroundJobs.Extensions;
using JC.Core.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimateMonopoly.Areas.Identity.Services;
using UltimateMonopoly.Areas.Social.Services;

using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Models.DataModels.Social;
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
            typeof(CustomBoardSpace),
            typeof(Friend),
            typeof(FriendRequest),
            typeof(BlockedUser),
            typeof(ReportedUser));

        services.TryAddSingleton<FilePathProvider>();

        services.TryAddScoped<BoardImportService>();
        services.TryAddScoped<BoardCacheService>();

        // Social — presence tracking
        services.TryAddSingleton<PresenceService>();
        services.AddHangfireJob<PresenceFlushJob>(opts =>
        {
            opts.Cron = "*/5 * * * *";
        });

        services.TryAddScoped<UrlLinkService>();

        // Social — friends
        services.TryAddScoped<FriendService>();
        services.TryAddScoped<BlockAndReportService>();

        // Identity — profile
        services.TryAddScoped<ProfileService>();

        return services;
    }
}