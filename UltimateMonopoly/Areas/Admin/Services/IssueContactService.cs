using System.Net;
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

    public IssueContactService(IRepositoryManager repos,
        UserManagementService users,
        IEmailService email,
        AdminLogService adminLog,
        IUserInfo userInfo)
    {
        _repos = repos;
        _users = users;
        _email = email;
        _adminLog = adminLog;

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

        // The simple overload uses the configured DefaultFromAddress / DefaultFromDisplayName.
        var result = await _email.SendAsync(
            new[] { new EmailRecipient(contact.Email, contact.DisplayName) },
            subject, plain, html);

        if (!result.Succeeded) return IssueContactResult.SendFailed;

        await _adminLog.LogIssueReporterContacted(issue.Id, issue.UserId, subject);
        return IssueContactResult.Sent;
    }

    /// <summary>Builds the plain-text + HTML bodies: greeting → the admin's message → the reporter's original
    /// report → sign-off + a short reference. Deliberately carries no GitHub link or client metadata.</summary>
    private static (string Plain, string Html) BuildBody(ReportedIssue issue, string displayName, string message)
    {
        var typeLabel = issue.Type == IssueType.Suggestion ? "suggestion" : "bug report";
        var date = issue.Created.ToLocalTime().ToString("D");
        var reference = issue.Id.Length >= 8 ? issue.Id[..8] : issue.Id;

        // The user's own report only — never the appended "View Issue in App" link (off the email to the client).
        var report = BugReportModel.StripReportLink(issue.Description);

        var plain =
            $"Hi {displayName},\n\n" +
            $"Thanks for the {typeLabel} you sent us on {date} — we appreciate you helping improve {RuleDictionary.GameName}.\n\n" +
            "----------------------------------------\n" +
            $"{message}\n" +
            "----------------------------------------\n\n" +
            "For reference, here's what you originally reported:\n" +
            $"  \"{report}\"\n\n" +
            $"— The {RuleDictionary.GameName} Team\n" +
            $"Reference: {reference}\n\n" +
            "You're receiving this because you submitted feedback in the app. If you need to add anything, just reply to this email.";

        // Inline styles only: email clients strip <style>/classes/CSS variables, so inline hex is the norm
        // for transactional email HTML (this is not app CSS, so the colour-variable convention doesn't apply).
        var html =
            "<div style=\"font-family: Arial, Helvetica, sans-serif; font-size: 15px; color: #1f2933; line-height: 1.5;\">" +
            $"<p>Hi {WebUtility.HtmlEncode(displayName)},</p>" +
            $"<p>Thanks for the {typeLabel} you sent us on {date} — we appreciate you helping improve {RuleDictionary.GameName}.</p>" +
            "<hr style=\"border: none; border-top: 1px solid #d9dee3; margin: 20px 0;\">" +
            $"<div>{HtmlParagraphs(message)}</div>" +
            "<hr style=\"border: none; border-top: 1px solid #d9dee3; margin: 20px 0;\">" +
            "<p style=\"color: #52606d; margin-bottom: 6px;\"><strong>For reference, here's what you originally reported:</strong></p>" +
            $"<blockquote style=\"margin: 0; padding: 10px 14px; border-left: 3px solid #c8d0d8; color: #52606d; background: #f5f7fa;\">{WebUtility.HtmlEncode(report)}</blockquote>" +
            $"<p style=\"margin-top: 20px;\">— The {RuleDictionary.GameName} Team<br>" +
            $"<span style=\"color: #7b8794; font-size: 12px;\">Reference: {reference}</span></p>" +
            "<p style=\"color: #7b8794; font-size: 12px;\">You're receiving this because you submitted feedback in the app. If you need to add anything, just reply to this email.</p>" +
            "</div>";

        return (plain, html);
    }

    /// <summary>Encodes free text for HTML, then maps blank lines to paragraphs and single newlines to &lt;br&gt;.</summary>
    private static string HtmlParagraphs(string text)
    {
        var encoded = WebUtility.HtmlEncode(text).Replace("\r\n", "\n");
        var paragraphs = encoded
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => $"<p>{p.Replace("\n", "<br>")}</p>");
        return string.Join("", paragraphs);
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