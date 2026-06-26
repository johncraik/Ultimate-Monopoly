using JC.Core.Models;
using JC.Identity.Authentication;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Composes the reusable Recent Activity panel (C1 §7.3 / §7.4) for a user. Every stream's scoped query +
/// table partial already exist (audit trail §9, log viewers §10, game management §8) — this builder just
/// fetches each in preview size and packs them into a <see cref="RecentActivityModel"/>, so Reports → Details
/// and (later) User → Details both compose the same panel. It owns no queries of its own.
/// <para>Admin- or SystemAdmin-gated (mirrors its dependencies). Recent games is composed only for a
/// SystemAdmin viewer (game management is SystemAdmin-only); admin logs are composed only when the user holds
/// an admin role or has ≥1 admin-log entry — every other stream is shown to any admin.</para>
/// </summary>
public class RecentActivityService
{
    // Preview sizes (C1 §7.4).
    private const int MessageSize = 15;
    private const int GamesSize = 10;
    private const int AdminSize = 5;
    private const int EmailSize = 5;
    private const int NotificationSize = 5;
    private const int TrailSize = 30;

    private readonly AppLogService _appLogs;
    private readonly GameManagementService _games;
    private readonly AuditTrailService _audit;

    public RecentActivityService(AppLogService appLogs, GameManagementService games, AuditTrailService audit,
        IUserInfo userInfo)
    {
        _appLogs = appLogs;
        _games = games;
        _audit = audit;

        if (!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    /// <summary>Builds the panel for <paramref name="userId"/>. Streams are keyed by id, so this works even
    /// for a now-deleted account (its historical logs/audit/games persist under the id until cleanup runs).
    /// <paramref name="viewerIsSystemAdmin"/> gates the Recent-games stream; <paramref name="userHoldsAdminRole"/>
    /// (with the ≥1-entry check) gates the Admin-logs stream.</summary>
    public async Task<RecentActivityModel> Build(string userId, bool viewerIsSystemAdmin, bool userHoldsAdminRole)
    {
        // Streams shown to any admin (gated ones empty until their feature lands — Message → E1, Email → A1).
        var messageLogs = await _appLogs.GetThreadActivityLogs(1, MessageSize, userId, null);
        var emailLogs = await _appLogs.GetEmailLogs(1, EmailSize, userId);
        var notifications = await _appLogs.GetUserNotifications(userId, 1, NotificationSize, null, null, null,
            NotificationStatusFilter.Active);
        var userTrail = await _audit.GetUserTrail(userId, 1, TrailSize, null, null);

        // Admin logs — gated: render only when the user currently holds an admin role OR has ≥1 entry (the
        // former-admin case). CreatedById = X = the actions X took as an admin.
        var adminLogsPaged = await _appLogs.GetAdminLogs(1, AdminSize, userId, null, null);
        var adminLogs = (userHoldsAdminRole || adminLogsPaged.TotalCount > 0)
            ? new AdminLogTableModel(adminLogsPaged, userId, null, null) { Preview = true }
            : null;

        // Recent games — SystemAdmin only (GetGames runs AuthCheck()); host OR player = X.
        GameTableModel? recentGames = null;
        if (viewerIsSystemAdmin)
        {
            var gamesPaged = await _games.GetGames(1, GamesSize, null, userId, null, null, userId);
            recentGames = new GameTableModel(gamesPaged, userId, null, null, null) { Preview = true };
        }

        return new RecentActivityModel(userId)
        {
            MessageLogs = new ThreadActivityLogTableModel(messageLogs, userId, null) { Preview = true },
            EmailLogs = new EmailLogTableModel(emailLogs, userId) { Preview = true },
            Notifications = new NotificationTableModel(notifications, userId, null, null, null,
                NotificationStatusFilter.Active) { Preview = true },
            UserTrail = new AuditTableModel(userTrail, null, null, isUserTrail: true) { Preview = true },
            AdminLogs = adminLogs,
            RecentGames = recentGames
        };
    }
}