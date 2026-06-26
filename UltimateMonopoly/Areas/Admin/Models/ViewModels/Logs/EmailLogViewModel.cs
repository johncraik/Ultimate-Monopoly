using JC.Communication.Email.Models;
using JC.Communication.Logging.Models.Email;
using JC.Core.Extensions;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>One outbound <see cref="EmailLog"/> (metadata only — this app logs <c>ExcludeContent</c> in prod,
/// so there is never a body). Carries the sender/subject, the resolved creating user, and its recipient +
/// send-attempt logs for the row's accordion.</summary>
public class EmailLogViewModel
{
    public string LogId { get; }
    public string FromAddress { get; }
    public string Subject { get; }

    public string CreatedById { get; } = "-";
    public string Username { get; } = "Unknown";

    public string CreatedDate { get; }
    public string RelativeDate { get; }

    public IReadOnlyList<EmailRecipientLogViewModel> Recipients { get; }
    public IReadOnlyList<EmailSentLogViewModel> SentLogs { get; }

    public EmailLogViewModel(EmailLog log, string? username = null)
    {
        LogId = log.Id;
        FromAddress = log.FromAddress;
        Subject = log.Subject;

        CreatedById = string.IsNullOrEmpty(log.CreatedById) ? "-" : log.CreatedById;
        Username = username ?? "Unknown";

        CreatedDate = log.CreatedUtc.ToLocalTime().ToString("g");
        RelativeDate = log.CreatedUtc.ToRelativeTime();

        Recipients = (log.EmailRecipientLogs ?? [])
            .Select(r => new EmailRecipientLogViewModel(r)).ToList();
        SentLogs = (log.EmailSentLogs ?? [])
            .OrderByDescending(s => s.SentAtUtc)
            .Select(s => new EmailSentLogViewModel(s)).ToList();
    }
}

/// <summary>One recipient of a logged email — its address, optional display name, and To/Cc/Bcc type.</summary>
public class EmailRecipientLogViewModel
{
    public string Address { get; }
    public string? DisplayName { get; }
    public RecipientLogType Type { get; }
    public string TypeDisplay { get; }

    public EmailRecipientLogViewModel(EmailRecipientLog recipient)
    {
        Address = recipient.Address;
        DisplayName = recipient.DisplayName;
        Type = recipient.RecipientLogType;
        TypeDisplay = recipient.RecipientLogType.ToDisplayName();
    }
}

/// <summary>One send attempt of a logged email — its result, provider, time, and any server response / error.</summary>
public class EmailSentLogViewModel
{
    public bool Succeeded { get; }
    public string ProviderDisplay { get; }
    public string SentAt { get; }
    public string RelativeSentAt { get; }
    public string? ServerResponse { get; }
    public string? ErrorMessage { get; }

    public EmailSentLogViewModel(EmailSentLog sent)
    {
        Succeeded = sent.Succeeded;
        ProviderDisplay = sent.Provider.ToDisplayName();
        SentAt = sent.SentAtUtc.ToLocalTime().ToString("g");
        RelativeSentAt = sent.SentAtUtc.ToRelativeTime();
        ServerResponse = sent.ServerResponse;
        ErrorMessage = sent.ErrorMessage;
    }
}
