using JC.Core.Extensions;
using JC.Github.Models;
using MP.GameEngine.Helpers.RuleSet;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Pages;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>Backs the issue-reporter Contact page — the issue context to show the admin, the resolved reporter
/// (current display name + email + confirmed state), and whether the reporter can actually be contacted (a
/// local account that still exists and has an email). When <see cref="CanContact"/> is false,
/// <see cref="BlockReason"/> says why.</summary>
public class IssueContactViewModel
{
    public string IssueId { get; }
    public IssueType Type { get; }
    public string TypeDisplay { get; }   // "Bug" / "Suggestion"
    public string TypeLabel { get; }     // prose form: "bug report" / "suggestion"

    public string Description { get; }
    public string CreatedDate { get; }
    public string RelativeDate { get; }

    public string ReporterId { get; }
    public string ReporterName { get; }
    public string? ReporterEmail { get; }
    public bool EmailConfirmed { get; }

    public bool CanContact { get; }
    public string? BlockReason { get; }

    public string DefaultSubject { get; }

    // Prior-contact history (from the AdminActionLog) — drives the "already contacted" warning.
    public int PreviousContactCount { get; }
    public bool AlreadyContacted => PreviousContactCount > 0;
    public string? LastContactedDate { get; }
    public string? LastContactedRelative { get; }
    public string? LastContactedBy { get; }

    public IssueContactViewModel(ReportedIssue issue, UserContactInfo? contact, IssueContactHistory? history = null)
    {
        IssueId = issue.Id;
        Type = issue.Type;
        TypeDisplay = issue.Type.ToDisplayName();
        TypeLabel = issue.Type == IssueType.Suggestion ? "suggestion" : "bug report";

        Description = BugReportModel.StripReportLink(issue.Description);
        CreatedDate = issue.Created.ToLocalTime().ToString("g");
        RelativeDate = issue.Created.ToRelativeTime();

        ReporterId = string.IsNullOrEmpty(issue.UserId) ? "-" : issue.UserId;
        DefaultSubject = $"Re: your {TypeLabel} on {RuleDictionary.GameName}";

        if (string.IsNullOrEmpty(issue.UserId))
        {
            // GitHub-origin issue — no local reporter to contact.
            ReporterName = "GitHub";
            CanContact = false;
            BlockReason = "This issue originated on GitHub, so there is no registered reporter to contact.";
        }
        else if (contact == null)
        {
            // The reporter's account has since been deleted (records orphan-by-id — design §6.3).
            ReporterName = !string.IsNullOrWhiteSpace(issue.UserDisplay) ? issue.UserDisplay : "Unknown";
            CanContact = false;
            BlockReason = "The reporter's account no longer exists.";
        }
        else
        {
            ReporterName = contact.DisplayName;
            ReporterEmail = contact.Email;
            EmailConfirmed = contact.EmailConfirmed;

            if (string.IsNullOrWhiteSpace(contact.Email))
            {
                CanContact = false;
                BlockReason = "The reporter has no email address on file.";
            }
            else
            {
                CanContact = true;
            }
        }

        PreviousContactCount = history?.Count ?? 0;
        if (history != null)
        {
            LastContactedDate = history.LastUtc.ToLocalTime().ToString("g");
            LastContactedRelative = history.LastUtc.ToRelativeTime();
            LastContactedBy = history.LastByUsername;
        }
    }
}

/// <summary>Prior "issue reporter contacted" admin-log activity for one issue — drives the Contact page's
/// duplicate-contact warning: how many times the reporter has been emailed about it, and the most recent
/// (when + which admin). Built by <c>IssueContactService</c> from the <c>AdminActionLog</c>.</summary>
public record IssueContactHistory(int Count, DateTime LastUtc, string? LastByUsername);