using JC.Communication.Email.Models;
using JC.Communication.Email.Services;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using JC.Github.Models;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Helpers.RuleSet;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Data;
using UltimateMonopoly.Pages;
using UltimateMonopoly.Helpers.Email;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// The issue-reporter contact flow (C1 — the Reported Issues page). Lets an admin (or a GithubManager) email
/// the local reporter of a bug / suggestion a support reply — fulfilling the "an admin responds to your
/// registered email" promise on the feedback modal. Composes the email (greeting → the admin's message → the
/// reporter's own original report; <b>no</b> GitHub link or client metadata, per the privacy posture), sends
/// it via <see cref="IEmailService"/> (which auto-logs to EmailLog), and records an <c>AdminActionLog</c>.
/// Admin- / SystemAdmin- / GithubManager-gated (mirrors the rest of the Issues area).
/// </summary>
public class IssueContactService
{
    private readonly IRepositoryManager _repos;
    private readonly UserManagementService _users;
    private readonly IEmailService _email;
    private readonly AdminLogService _adminLog;
    private readonly IConfiguration _config;

    public IssueContactService(IRepositoryManager repos,
        UserManagementService users,
        IEmailService email,
        AdminLogService adminLog,
        IConfiguration config,
        IUserInfo userInfo)
    {
        _repos = repos;
        _users = users;
        _email = email;
        _adminLog = adminLog;
        _config = config;

        if (!userInfo.IsInRole(SystemRoles.Admin)
            && !userInfo.IsInRole(SystemRoles.SystemAdmin)
            && !userInfo.IsInRole(AppRoles.GithubManager))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    private async Task<ReportedIssue?> GetIssue(string issueId)
        => await _repos.GetRepository<ReportedIssue>()
            .AsQueryable().AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == issueId);

    /// <summary>The Contact page's context for <paramref name="issueId"/> — the issue plus the resolved
    /// reporter (current display name / email / confirmed state). Returns null when the issue doesn't exist
    /// (the page 404s).</summary>
    public async Task<IssueContactViewModel?> GetContext(string issueId)
    {
        var issue = await GetIssue(issueId);
        if (issue == null) return null;

        var contact = string.IsNullOrEmpty(issue.UserId) ? null : await _users.GetContactInfo(issue.UserId);
        var history = await GetContactHistory(issueId);
        return new IssueContactViewModel(issue, contact, history);
    }

    /// <summary>Prior contact attempts for an issue, read from the AdminActionLog (every send writes an
    /// <see cref="AdminActionType.IssueReporterContacted"/> entry). Drives the duplicate-contact warning, so the
    /// future admin-log retention job must not prune these entries (see <c>AdminLogService</c>).</summary>
    private async Task<IssueContactHistory?> GetContactHistory(string issueId)
    {
        var logs = await _repos.GetRepository<AdminActionLog>()
            .AsQueryable().AsNoTracking()
            .Where(l => l.Action == AdminActionType.IssueReporterContacted && l.TargetId == issueId)
            .OrderByDescending(l => l.CreatedUtc)
            .ToListAsync();

        if (logs.Count == 0) return null;

        var last = logs[0];
        var by = string.IsNullOrEmpty(last.CreatedById)
            ? null
            : (await _users.GetUserById(last.CreatedById))?.Profile.Username;

        return new IssueContactHistory(logs.Count, last.CreatedUtc, by);
    }

    /// <summary>Emails the issue's reporter the admin's <paramref name="message"/> under
    /// <paramref name="subject"/>, then logs the action. The result says whether it sent (and if not, why) —
    /// every "can't contact" case is re-checked here, so a stale form can't send to a now-deleted reporter.</summary>
    public async Task<IssueContactResult> SendContact(string issueId, string subject, string message)
    {
        var issue = await GetIssue(issueId);
        if (issue == null) return IssueContactResult.IssueMissing;
        if (string.IsNullOrEmpty(issue.UserId)) return IssueContactResult.NoReporter;

        var contact = await _users.GetContactInfo(issue.UserId);
        if (contact == null) return IssueContactResult.ReporterMissing;
        if (string.IsNullOrWhiteSpace(contact.Email)) return IssueContactResult.NoEmail;

        subject = string.IsNullOrWhiteSpace(subject) ? $"Re: your report on {RuleDictionary.GameName}" : subject.Trim();
        var (plain, html) = BuildBody(issue, contact.DisplayName, message.Trim());

        // Send from the support mailbox (same address contact messages go to), not the noreply DefaultFromAddress —
        // the body invites the reporter to "just reply to this email", so replies must land in support.
        var from = _config["Communication:Email:ContactRecipient"] ?? "support@monappoly.com";
        var messageToSend = new EmailMessage(
            from, htmlBody: html, plainBody: plain, subject: subject,
            new EmailRecipient(contact.Email, contact.DisplayName));
        var result = await _email.SendAsync(messageToSend);

        if (!result.Succeeded) return IssueContactResult.SendFailed;

        await _adminLog.LogIssueReporterContacted(issue.Id, issue.UserId, subject);
        return IssueContactResult.Sent;
    }

    /// <summary>Builds the plain-text + HTML bodies via <see cref="EmailBuilder"/>: greeting → the admin's message
    /// → the reporter's original report → sign-off + a short reference. Deliberately carries no GitHub link or
    /// client metadata.</summary>
    private static (string Plain, string Html) BuildBody(ReportedIssue issue, string displayName, string message)
    {
        var typeLabel = issue.Type == IssueType.Suggestion ? "suggestion" : "bug report";
        var date = issue.Created.ToLocalTime().ToString("D");
        var reference = issue.Id.Length >= 8 ? issue.Id[..8] : issue.Id;

        // The user's own report only — never the appended "View Issue in App" link (off the email to the client).
        var report = BugReportModel.StripReportLink(issue.Description);

        return EmailBuilder.Create("Support")
            .Paragraph($"Hi {displayName},")
            .Paragraph($"Thanks for the {typeLabel} you sent us on {date} — we appreciate you helping improve {RuleDictionary.GameName}.")
            .Quote(message)
            .Paragraph("For reference, here's what you originally reported:", emphasis: true)
            .Quote(report)
            .SignOff($"— The {RuleDictionary.GameName} Team")
            .Reference(reference)
            .Footer("You're receiving this because you submitted feedback in the app. If you need to add anything, just reply to this email.")
            .Build();
    }
}

public enum IssueContactResult
{
    Sent,
    IssueMissing,
    NoReporter,
    ReporterMissing,
    NoEmail,
    SendFailed
}