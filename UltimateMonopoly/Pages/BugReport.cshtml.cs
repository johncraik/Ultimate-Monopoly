using JC.Communication.Notifications.Models;
using JC.Communication.Notifications.Services;
using JC.Core.Models;
using JC.Github.Models;
using JC.Github.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Data;
using UltimateMonopoly.Services;

namespace UltimateMonopoly.Pages;

/// <summary>AJAX endpoint behind the bug-reporter / feedback widgets — the dev floating widget (non-prod) and
/// the production "Give Feedback" modal both post here. Records a bug / suggestion locally and pushes it to
/// GitHub via <see cref="BugReportService"/>.</summary>
public class BugReportModel : PageModel
{
    private readonly BugReportService _bugReports;
    private readonly IUserInfo _userInfo;
    private readonly NotificationSender _notifications;
    private readonly UserService _userService;

    public BugReportModel(BugReportService bugReports,
        IUserInfo userInfo,
        NotificationSender notifications,
        UserService userService)
    {
        _bugReports = bugReports;
        _userInfo = userInfo;
        _notifications = notifications;
        _userService = userService;
    }

    // No direct page — the widgets post here via fetch.
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync([FromBody] BugReportInput input)
    {
        var description = input?.Description?.Trim() ?? "";
        if (description.Length == 0)
            return BadRequest(new { error = "Please describe the issue." });

        // The widget sends "bug" / "suggestion"; anything that isn't a suggestion is treated as a bug.
        var type = string.Equals(input!.Type, "suggestion", StringComparison.OrdinalIgnoreCase)
            ? IssueType.Suggestion
            : IssueType.Bug;

        // creatorId / creatorName are stored locally only (not sent to GitHub) — see JC.Github guide.
        var creatorName = "Unknown";
        if(!string.IsNullOrWhiteSpace(_userInfo.DisplayName))
            creatorName = _userInfo.DisplayName;
        else if(!string.IsNullOrWhiteSpace(_userInfo.Username))
            creatorName = _userInfo.Username;
        else if(!string.IsNullOrWhiteSpace(_userInfo.Email))
            creatorName = _userInfo.Email;

        var issue = await _bugReports.RecordIssue(description, type, _userInfo.UserId, creatorName, input.Metadata);

        // GitHub assigns the issue number only on creation, so now that it exists, append a "View Issue in App"
        // deep link back to the admin issues page (anchored to this issue's row via #{ExternalId}). UpdateIssueBody
        // syncs both GitHub and the local copy.
        if (issue.ReportSent && issue.ExternalId.HasValue)
        {
            var appUrl = $"{Request.Scheme}://{Request.Host}/Admin/Logs/Issues/Index#{issue.ExternalId}";
            var linkedBody = $"{description}\n\n<p><a href=\"{appUrl}\">View Issue in App</a></p>";
            await _bugReports.UpdateIssueBody(issue, linkedBody);
        }

        if (issue.ReportSent)
        {
            // Notify everyone holding the GithubManager role (enabled users) of the new report.
            var typeLabel = type == IssueType.Suggestion ? "suggestion" : "bug";
            var link = issue.ExternalId.HasValue
                ? $"/Admin/Logs/Issues/Index#{issue.ExternalId}"
                : "/Admin/Logs/Issues/Index";

            foreach (var managerId in await _userService.GetUserIdsInRole(AppRoles.GithubManager))
            {
                await _notifications.SendNotification(
                    managerId,
                    title: $"New {typeLabel} reported",
                    body: $"{creatorName} submitted a new {typeLabel}.",
                    type: NotificationType.Info,
                    link: link);
            }
        }

        return new JsonResult(new { reportSent = issue.ReportSent, externalId = issue.ExternalId });
    }

    /// <summary>Payload posted by the JC.Web <c>&lt;bug-reporter&gt;</c> tag helper.</summary>
    public class BugReportInput
    {
        public string? Type { get; set; }
        public string? Description { get; set; }
        public string? Metadata { get; set; }
    }
}
