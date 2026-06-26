using System.Text.Json;
using JC.Core.Extensions;
using JC.Github.Models;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>One locally-persisted <see cref="ReportedIssue"/> (bug / suggestion) — its type, status, GitHub
/// sync state + link, reporter, optional screenshot, and synced comments for the row's accordion.</summary>
public class ReportedIssueViewModel
{
    public string IssueId { get; }
    public IssueType Type { get; }
    public string TypeDisplay { get; }
    
    public string Description { get; }
    public Dictionary<string, object?> ClientMetadata { get; } = new();

    public bool ReportSent { get; }
    public int? ExternalId { get; }
    public string? GitHubUrl { get; }

    public bool Closed { get; }

    public string CreatedDate { get; }
    public string RelativeDate { get; }

    public string ReporterId { get; } = "-";
    public string ReporterName { get; }

    public bool HasImage { get; }
    public string? ImageDataUri { get; }

    public IReadOnlyList<IssueCommentViewModel> Comments { get; }

    public ReportedIssueViewModel(ReportedIssue issue, string? gitHubUrl, 
        IReadOnlyList<IssueCommentViewModel> comments)
    {
        IssueId = issue.Id;
        Type = issue.Type;
        TypeDisplay = issue.Type.ToDisplayName();

        Description = issue.Description;
        if (!string.IsNullOrEmpty(issue.ClientMetadata))
        {
            try
            {
                ClientMetadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(issue.ClientMetadata) 
                                 ?? new Dictionary<string, object?>();
            }
            catch { /*ignored*/ }
        }

        ReportSent = issue.ReportSent;
        ExternalId = issue.ExternalId;
        GitHubUrl = gitHubUrl;

        Closed = issue.Closed;

        CreatedDate = issue.Created.ToLocalTime().ToString("g");
        RelativeDate = issue.Created.ToRelativeTime();

        // UserDisplay is the report-time name; UserId is the local reporter. A null UserId means the issue
        // originated on GitHub (webhook sync), where there is no local reporter.
        ReporterId = string.IsNullOrEmpty(issue.UserId) ? "-" : issue.UserId;
        ReporterName = !string.IsNullOrWhiteSpace(issue.UserDisplay)
            ? issue.UserDisplay
            : string.IsNullOrEmpty(issue.UserId) ? "GitHub" : "Unknown";

        HasImage = issue.Image is { Length: > 0 };
        ImageDataUri = HasImage ? $"data:image/png;base64,{Convert.ToBase64String(issue.Image!)}" : null;

        Comments = comments;
    }
}

/// <summary>One GitHub <see cref="IssueComment"/> synced via webhook — author (GitHub login), body, and
/// created/edited timestamps.</summary>
public class IssueCommentViewModel
{
    public long CommentId { get; }
    public string Author { get; }
    public string Body { get; }
    public string CreatedAt { get; }
    public bool IsEdited { get; }
    public string? EditedAt { get; }

    public IssueCommentViewModel(IssueComment comment)
    {
        CommentId = comment.CommentId;
        Author = comment.Author;
        Body = comment.Body;
        CreatedAt = comment.CreatedAt.ToLocalTime().ToString("g");
        IsEdited = comment.UpdatedAt.HasValue;
        EditedAt = comment.UpdatedAt?.ToLocalTime().ToString("g");
    }
}
