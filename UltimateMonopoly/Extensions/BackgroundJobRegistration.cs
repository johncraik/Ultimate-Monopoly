using JC.BackgroundJobs.Extensions;
using JC.BackgroundJobs.Models;
using JC.Communication.Email.Services;
using JC.Communication.Messaging.Services;
using JC.Communication.Notifications.Services;
using JC.Core.Services;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Services.Games;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Extensions;

public static class BackgroundJobRegistration
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        var uk = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        
        // Game retention (terminal hard-purge) — permanently deletes ANY soft-deleted game-history record
        // (snapshots/events/turns) past GameSettings.CleanupRetentionMonths: both the cascade from a deleted
        // game AND the snapshots the snapshot auto-delete job soft-deletes on active games. Game/GamePlayer
        // shells are kept (PlayerGameStat FK → stats stay intact). Daily at 03:00 UK, after the 01:00 stats sweep.
        services.AddHangfireJob<GameCleanupJob>(opts =>
        {
            opts.Cron = "0 3 * * *";
            opts.TimeZone = uk;
        });

        // Abandoned games — in-play games with no new turn for GameSettings.AbandonedRetentionWeeks get
        // Cancelled or Drawn (per GameSettings.AbandonedGameAction). Daily at 04:00 UK.
        services.AddHangfireJob<GameAbandonmentJob>(opts =>
        {
            opts.Cron = "0 4 * * *";
            opts.TimeZone = uk;
        });

        // Auto-delete cancelled games — soft-deletes Cancelled games last touched past
        // GameSettings.AutoDeleteCancelledRetentionMonths (the GameCleanupJob later hard-purges them). 03:30 UK.
        services.AddHangfireJob<CancelledGameCleanupJob>(opts =>
        {
            opts.Cron = "30 3 * * *";
            opts.TimeZone = uk;
        });

        // Auto-delete snapshots — soft-deletes the snapshots/events of finished/cancelled games past
        // GameSettings.AutoDeleteSnapshotsRetentionMonths (off by default). 04:30 UK.
        services.AddHangfireJob<SnapshotCleanupJob>(opts =>
        {
            opts.Cron = "30 4 * * *";
            opts.TimeZone = uk;
        });
        
        
        //Stats job
        services.AddHangfireScheduler(AdHocJobRegistration.For<StatisticsJob>());
        services.AddHangfireJob<StatisticsJob>(opts =>
        {
            opts.Cron = "0 1,13 * * *";
            opts.TimeZone = uk;
        });

        // Daily user-activity snapshot — powers the dashboard's logins / DAU / WAU / MAU trend history
        // (the live tables store only the latest activity, not history). 00:10 UK — records the prior day.
        services.AddHangfireJob<DailyStatsJob>(opts =>
        {
            opts.Cron = "10 0 * * *";
            opts.TimeZone = uk;
        });
        
        
        
        // ---- Log & audit retention ----
        // The custom AdminActionLog cleanup plus every JC-package cleanup job, registered on the Hangfire
        // recurring path. Each package job is internally gated by its own options (set in Program.cs), so a
        // disabled one no-ops. Staggered in the early-morning UK window, clear of the 03:00–04:30 game jobs.
        
        // Admin-action log (custom, C1 §14) — hard-purges entries past 6 months, always keeping
        // IssueReporterContacted (the duplicate-contact warning depends on them). 02:30 UK.
        services.AddHangfireJob<AdminLogCleanupJob>(opts => { opts.Cron = "30 2 * * *"; opts.TimeZone = uk; });

        // JC.Core — audit-entry retention (configured via ConfigureCoreBackgroundJobs). 02:00 UK.
        services.AddHangfireJob<AuditCleanupJob>(opts => { opts.Cron = "0 2 * * *"; opts.TimeZone = uk; });
        // JC.Core — generic soft-delete reclaim. Registered for completeness but DISABLED by default
        // (EnableSoftDeleteCleanupJob = false in Program.cs): its app-wide auto-discovery would bypass the
        // bespoke game-retention pipeline. No-ops while disabled. 02:15 UK.
        //services.AddHangfireJob<SoftDeleteCleanupJob>(opts => { opts.Cron = "15 2 * * *"; opts.TimeZone = uk; });

        // JC.Communication — comms-log retention (configured via Configure*BackgroundJobs in Program.cs). 05:00–05:45 UK.
        services.AddHangfireJob<EmailLogCleanupJob>(opts => { opts.Cron = "0 5 * * *"; opts.TimeZone = uk; });
        services.AddHangfireJob<NotificationLogCleanupJob>(opts => { opts.Cron = "15 5 * * *"; opts.TimeZone = uk; });
        services.AddHangfireJob<ActivityLogCleanupJob>(opts => { opts.Cron = "30 5 * * *"; opts.TimeZone = uk; });
        services.AddHangfireJob<ReadLogCleanupJob>(opts => { opts.Cron = "45 5 * * *"; opts.TimeZone = uk; });
        
        return services;
    }
}