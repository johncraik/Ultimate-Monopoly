using JC.BackgroundJobs.Extensions;
using JC.BackgroundJobs.Models;
using JC.Core.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Extensions;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.DataModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.BoardSkins;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Friends;
using UltimateMonopoly.Services.GameEngine;
using UltimateMonopoly.Services.Games;
using UltimateMonopoly.Services.Imports;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Extensions;

public static class ServiceRegistration
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.RegisterRepositoryContexts(
            typeof(BlockedWord),
            typeof(BoardSkin),
            typeof(BoardSkinSpace),
            typeof(SharedBoardSkin),
            typeof(Friend),
            typeof(FriendRequest),
            typeof(BlockedUser),
            typeof(ReportedUser),
            typeof(Game),
            typeof(GamePlayer),
            typeof(GameTurn),
            typeof(GameSnapshot),
            typeof(GameTurnEvents),
            typeof(PlayerGameStat),
            typeof(PersistedCardIds));

        services.TryAddSingleton<FilePathProvider>();
        services.TryAddScoped<UserService>();

        services.TryAddScoped<BoardImportService>();
        services.TryAddScoped<BoardCacheService>();
        services.TryAddScoped<BoardSkinService>();
        services.TryAddScoped<BoardSkinShareService>();

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
        services.TryAddScoped<PlayerCacheService>();

        // Content safety — profanity filtering (B1). Library filter is immutable once built → singleton;
        // the cache + orchestrator do a (cached) DB read → scoped. Normaliser is a static helper.
        services.TryAddSingleton<ProfanityFilter.Interfaces.IProfanityFilter, ProfanityFilter.ProfanityFilter>();
        services.TryAddScoped<BlockedWordsCacheService>();
        services.TryAddScoped<ProfanityService>();
        services.TryAddScoped<BlockedWordImportService>();

        // Games
        services.TryAddScoped<GameSetupService>();
        services.TryAddScoped<GameService>();
        services.TryAddScoped<PlayerProfileService>();
        services.TryAddScoped<GameCacheService>();
        
        // Cards
        services.TryAddScoped<CardCacheService>();
        services.TryAddScoped<CardImportService>();
        
        // Game Engine
        services.TryAddScoped<IGameCompletionService, GameCompletionService>();
        services.TryAddScoped<ISnapshotService, SnapshotService>();
        services.TryAddScoped<IGameEngineFactory, GameEngineFactory>();
        services.TryAddSingleton<IEngineNotifier, SignalrEngineNotifier>();
        services.TryAddSingleton<IGameExecutor, GameExecutor>();
        services.AddGameEngine();

        // Statistics — the per-game projection. Enqueued fire-and-forget by GameStatsService when
        // a game concludes (the ad-hoc scheduler registration), and also run on a recurring
        // schedule as a safety-net: it sweeps every finished game and is idempotent, so it
        // backfills any game whose stats never got written. 01:00 and 13:00 UK time (every 12h).
        services.TryAddScoped<GameStatsService>();
        services.TryAddScoped<LeaderboardService>();
        services.AddHangfireScheduler(AdHocJobRegistration.For<StatisticsJob>());
        services.AddHangfireJob<StatisticsJob>(opts =>
        {
            opts.Cron = "0 1,13 * * *";
            opts.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        });

        // Rules
        services.TryAddSingleton<RuleCatalog>();

        return services;
    }
}