using JC.Communication.Logging.Models.Email;
using JC.Communication.Logging.Models.Messaging;
using JC.Communication.Logging.Models.Notifications;
using JC.Communication.Notifications.Models;
using JC.Communication.Notifications.Services;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Pagination;
using System.Net;
using System.Text.RegularExpressions;
using JC.Core.Services.DataRepositories;
using JC.Github.Models;
using JC.Github.Models.Options;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Read-only viewer over the application's logs (C1 §10) — starting with the <see cref="AdminActionLog"/>
/// accountability trail; the comms logs (notifications / email / messaging) slot in here later. One service
/// for all app logs (not a service per log type). Admin- or SystemAdmin-gated (logs are read-only moderation,
/// both tiers).
/// </summary>
public class AppLogService
{
    private readonly IRepositoryManager _repos;
    private readonly UserManagementService _users;
    private readonly NotificationService _notifications;
    private readonly GithubOptions _githubOptions;

    public AppLogService(IRepositoryManager repos, 
        UserManagementService users,
        NotificationService notifications,
        IOptions<GithubOptions> githubOptions,
        IUserInfo userInfo)
    {
        _repos = repos;
        _users = users;
        _notifications = notifications;
        _githubOptions = githubOptions.Value;
        
        if (!userInfo.IsInRole(SystemRoles.Admin) 
            && !userInfo.IsInRole(SystemRoles.SystemAdmin)
            && !userInfo.IsInRole(AppRoles.GithubManager))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    /// <summary>Resolves each distinct user id to a username once (the logs share recipients), so the table
    /// doesn't re-query the same user per row.</summary>
    private async Task<Dictionary<string, string>> ResolveUsernames(IEnumerable<string> userIds)
    {
        var names = new Dictionary<string, string>();
        foreach (var id in userIds.Distinct())
        {
            if (string.IsNullOrEmpty(id) || names.ContainsKey(id)) continue;
            var user = await _users.GetUserById(id);
            names[id] = user?.Profile.Username ?? "Unknown";
        }
        return names;
    }

    /// <summary>Admin-action log entries newest-first, optionally filtered by action, target type, and a
    /// search over the acting admin id, target id, or detail text.</summary>
    private IQueryable<AdminActionLog> QueryAdminLogs(string? search, AdminActionType? action, AdminTargetType? targetType)
    {
        var query = _repos.GetRepository<AdminActionLog>()
            .AsQueryable().AsNoTracking();

        if (action.HasValue)
            query = query.Where(l => l.Action == action.Value);

        if (targetType.HasValue)
            query = query.Where(l => l.TargetType == targetType.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(l => (l.CreatedById != null && l.CreatedById.ToLower().Contains(search))
                                     || (l.TargetId != null && l.TargetId.ToLower().Contains(search))
                                     || (l.Detail != null && l.Detail.ToLower().Contains(search)));
        }

        return query.OrderByDescending(l => l.CreatedUtc);
    }

    public async Task<PagedList<AdminLogViewModel>> GetAdminLogs(int pageNumber, int pageSize, string? search,
        AdminActionType? action, AdminTargetType? targetType)
    {
        var paged = await QueryAdminLogs(search, action, targetType).ToPagedListAsync(pageNumber, pageSize);

        var viewModels = new List<AdminLogViewModel>();
        foreach (var log in paged)
        {
            // Resolve the acting admin (CreatedById) for the Admin column; the rich VM ctor also carries
            // whether they currently hold an admin role. A now-deleted / unresolvable actor → the basic VM
            // (Unknown actor, null current-admin → a grey minus in the table).
            var actor = string.IsNullOrEmpty(log.CreatedById) ? null : await _users.GetUserById(log.CreatedById);
            viewModels.Add(actor == null
                ? new AdminLogViewModel(log)
                : new AdminLogViewModel(log, actor.Profile.Username, actor.Profile.UserId,
                    actor.Roles.Contains(SystemRoles.Admin) || actor.Roles.Contains(SystemRoles.SystemAdmin)));
        }

        return new PagedList<AdminLogViewModel>(viewModels, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }

    // ---- Notification logs (read/unread events) ----
    // NotificationLog is a LogModel with no JC read service (NotificationLogService only writes), so it's read
    // via the repository — the same pattern as the AdminActionLog reads above.

    /// <summary>All notification read/unread events newest-first, optionally filtered by read state and a
    /// search over the acting user id / notification id (the global Logs page).</summary>
    public async Task<PagedList<NotificationLogViewModel>> GetNotificationLogs(int pageNumber, int pageSize,
        string? search, bool? read)
    {
        var query = _repos.GetRepository<NotificationLog>()
            .AsQueryable().AsNoTracking();

        if (read.HasValue)
            query = query.Where(l => l.IsRead == read.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(l => l.UserId.ToLower().Contains(search) || l.NotificationId.ToLower().Contains(search));
        }

        var paged = await query.OrderByDescending(l => l.CreatedUtc).ToPagedListAsync(pageNumber, pageSize);
        var names = await ResolveUsernames(paged.Select(l => l.UserId));
        var vms = paged.Select(l => new NotificationLogViewModel(l, names.GetValueOrDefault(l.UserId))).ToList();
        return new PagedList<NotificationLogViewModel>(vms, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }

    // ---- Reported issues (JC.Github) ----
    // ReportedIssue / IssueComment are domain entities (not LogModel, not AuditModel) — query the registered
    // repositories directly; ReportedIssue uses a Closed bool (no soft-delete), IssueComment a Deleted bool.

    /// <summary>Reported issues (bugs / suggestions) newest-first, optionally filtered by type, open/closed
    /// status, and a search over the description / reporter, with each synced issue's GitHub comments attached.</summary>
    public async Task<PagedList<ReportedIssueViewModel>> GetReportedIssues(int pageNumber, int pageSize,
        string? search, IssueType? type, bool? closed)
    {
        var query = _repos.GetRepository<ReportedIssue>()
            .AsQueryable().AsNoTracking();

        if (type.HasValue)
            query = query.Where(i => i.Type == type.Value);

        if (closed.HasValue)
            query = query.Where(i => i.Closed == closed.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(i => i.Description.ToLower().Contains(search)
                                     || (i.UserDisplay != null && i.UserDisplay.ToLower().Contains(search))
                                     || (i.UserId != null && i.UserId.ToLower().Contains(search)));
        }

        var paged = await query.OrderByDescending(i => i.Created).ToPagedListAsync(pageNumber, pageSize);

        // Comments for the page's synced issues only (IssueComment.IssueNumber == ReportedIssue.ExternalId).
        var externalIds = paged.Where(i => i.ExternalId.HasValue).Select(i => i.ExternalId!.Value).ToList();
        var commentsByIssue = await CommentsByIssueNumber(externalIds);

        // The body is stored as HTML. Re-sanitise before render (covers GitHub-synced bodies we didn't build),
        // and derive a plain-text preview for the table column.
        var vms = paged.Select(i => new ReportedIssueViewModel(i,
            BuildGitHubUrl(i.ExternalId), i.ExternalId.HasValue 
                ? commentsByIssue.GetValueOrDefault(i.ExternalId.Value) ?? [] 
                : [])).ToList();
        return new PagedList<ReportedIssueViewModel>(vms, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }

    /// <summary>Plain-text lead of an issue body for the table column — the user's free text (everything before
    /// the first metadata <c>&lt;details&gt;</c> block), with tags stripped and whitespace collapsed.</summary>
    private static string BuildPreview(string descriptionHtml)
    {
        var idx = descriptionHtml.IndexOf("<details", StringComparison.OrdinalIgnoreCase);
        var lead = idx >= 0 ? descriptionHtml[..idx] : descriptionHtml;
        var text = WebUtility.HtmlDecode(Regex.Replace(lead, "<.*?>", " "));
        return Regex.Replace(text, "\\s+", " ").Trim();
    }

    /// <summary>The browser URL for a synced issue (<c>github.com/{owner}/{repo}/issues/{number}</c>), or null
    /// when the issue never synced or the repo owner/name aren't configured.</summary>
    private string? BuildGitHubUrl(int? externalId)
        => externalId.HasValue
           && !string.IsNullOrEmpty(_githubOptions.GithubRepoOwner)
           && !string.IsNullOrEmpty(_githubOptions.GithubRepoName)
            ? $"https://github.com/{_githubOptions.GithubRepoOwner}/{_githubOptions.GithubRepoName}/issues/{externalId.Value}"
            : null;

    /// <summary>One batched query of the (non-deleted) comments for a set of GitHub issue numbers, grouped.</summary>
    private async Task<Dictionary<int, List<IssueCommentViewModel>>> CommentsByIssueNumber(List<int> issueNumbers)
    {
        if (issueNumbers.Count == 0) return new Dictionary<int, List<IssueCommentViewModel>>();

        var comments = await _repos.GetRepository<IssueComment>()
            .AsQueryable().AsNoTracking()
            .Where(c => issueNumbers.Contains(c.IssueNumber) && !c.Deleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return comments
            .GroupBy(c => c.IssueNumber)
            .ToDictionary(g => g.Key, g => g.Select(c => new IssueCommentViewModel(c)).ToList());
    }

    // ---- Messaging: thread activity logs ----
    // Read via the repository (no JC read service — MessagingLogService only writes). MessageReadLog is
    // deliberately NOT surfaced here: it's read-receipt state ("read @ {time}") for the chat UI, not an
    // admin activity log.

    /// <summary>Thread activity events (message sends, participant add/remove) newest-first, optionally
    /// filtered by activity type and a search over the thread id / acting-user id / details.</summary>
    public async Task<PagedList<ThreadActivityLogViewModel>> GetThreadActivityLogs(int pageNumber, int pageSize,
        string? search, ThreadActivityType? activityType)
    {
        var query = _repos.GetRepository<ThreadActivityLog>()
            .AsQueryable().AsNoTracking();

        if (activityType.HasValue)
            query = query.Where(l => l.ActivityType == activityType.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(l => l.ThreadId.ToLower().Contains(search)
                                     || (l.CreatedById != null && l.CreatedById.ToLower().Contains(search))
                                     || (l.ActivityDetails != null && l.ActivityDetails.ToLower().Contains(search)));
        }

        var paged = await query.OrderByDescending(l => l.ActivityTimestampUtc).ToPagedListAsync(pageNumber, pageSize);
        var names = await ResolveUsernames(paged.Select(l => l.CreatedById ?? ""));
        var vms = paged.Select(l => new ThreadActivityLogViewModel(l, names.GetValueOrDefault(l.CreatedById ?? ""))).ToList();
        return new PagedList<ThreadActivityLogViewModel>(vms, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }

    // ---- A user's notifications (option A) — via NotificationService, with each notification's logs attached ----

    /// <summary>A user's notifications for the chosen <paramref name="status"/> view (each status maps to a
    /// distinct <see cref="NotificationService"/> method), filtered by type / read-state / search, with each
    /// notification's read/unread logs attached for its accordion.</summary>
    public async Task<PagedList<NotificationViewModel>> GetUserNotifications(string userId, int pageNumber, int pageSize,
        string? search, NotificationType? type, bool? read, NotificationStatusFilter status)
    {
        var list = status switch
        {
            NotificationStatusFilter.Expired => await _notifications.GetExpiredNotifications(userId: userId),
            NotificationStatusFilter.Dismissed => await _notifications.GetNotifications(userId: userId, deletedQueryType: DeletedQueryType.OnlyDeleted),
            _ => await _notifications.GetNotifications(userId: userId, deletedQueryType: DeletedQueryType.OnlyActive)
        };

        if (type.HasValue)
            list = list.Where(n => n.Type == type.Value).ToList();
        if (read.HasValue)
            list = list.Where(n => n.IsRead == read.Value).ToList();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            list = list.Where(n => n.Title.ToLower().Contains(s) || n.Body.ToLower().Contains(s)).ToList();
        }

        // Paginate first, then load logs only for this page's notifications.
        var paged = list.ToPagedList(pageNumber, pageSize);
        var logsByNotification = await NotificationLogsByNotification(paged.Select(n => n.Id).ToList());

        var vms = paged
            .Select(n => new NotificationViewModel(n, logsByNotification.GetValueOrDefault(n.Id) ?? []))
            .ToList();
        return new PagedList<NotificationViewModel>(vms, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }

    // ---- Email logs (metadata only) ----
    // Read via the repository (no JC read service — EmailLogService only writes). The body (EmailContentLog)
    // is never touched: this app logs ExcludeContent in prod, so there's no content to show anyway.

    /// <summary>Outbound email logs newest-first, optionally filtered by a search over the from / subject /
    /// creating-user id / recipient address, with each log's recipients and send attempts loaded.</summary>
    public async Task<PagedList<EmailLogViewModel>> GetEmailLogs(int pageNumber, int pageSize, string? search)
    {
        var query = _repos.GetRepository<EmailLog>()
            .AsQueryable().AsNoTracking()
            .Include(e => e.EmailRecipientLogs)
            .Include(e => e.EmailSentLogs)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(e => e.FromAddress.ToLower().Contains(search)
                                     || e.Subject.ToLower().Contains(search)
                                     || (e.CreatedById != null && e.CreatedById.ToLower().Contains(search))
                                     || e.EmailRecipientLogs.Any(r => r.Address.ToLower().Contains(search)));
        }

        var paged = await query.OrderByDescending(e => e.CreatedUtc).ToPagedListAsync(pageNumber, pageSize);
        var names = await ResolveUsernames(paged.Select(e => e.CreatedById ?? ""));
        var vms = paged.Select(e => new EmailLogViewModel(e, names.GetValueOrDefault(e.CreatedById ?? ""))).ToList();
        return new PagedList<EmailLogViewModel>(vms, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }

    /// <summary>One batched query of the read/unread logs for a set of notifications, grouped by notification id.</summary>
    private async Task<Dictionary<string, List<NotificationLogViewModel>>> NotificationLogsByNotification(List<string> notificationIds)
    {
        if (notificationIds.Count == 0) return new Dictionary<string, List<NotificationLogViewModel>>();

        var logs = await _repos.GetRepository<NotificationLog>()
            .AsQueryable().AsNoTracking()
            .Where(l => notificationIds.Contains(l.NotificationId))
            .OrderByDescending(l => l.CreatedUtc)
            .ToListAsync();

        var names = await ResolveUsernames(logs.Select(l => l.UserId));
        return logs
            .GroupBy(l => l.NotificationId)
            .ToDictionary(g => g.Key,
                g => g.Select(l => new NotificationLogViewModel(l, names.GetValueOrDefault(l.UserId))).ToList());
    }
}
