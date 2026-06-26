using JC.Communication.Logging.Models.Messaging;
using JC.Core.Extensions;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

/// <summary>One <see cref="ThreadActivityLog"/> event — a message send or participant add/remove in a chat
/// thread. Metadata only: <c>ActivityDetails</c> is a generated string ("Message from {id}", "Participant(s)
/// added: …"), never message content.</summary>
public class ThreadActivityLogViewModel
{
    public string LogId { get; }
    public string ThreadId { get; }

    public ThreadActivityType ActivityType { get; }
    public string ActivityTypeDisplay { get; }

    public string Details { get; }

    public string CreatedById { get; } = "-";
    public string Username { get; } = "Unknown";

    public string ActivityDate { get; }
    public string RelativeDate { get; }

    public ThreadActivityLogViewModel(ThreadActivityLog log, string? username = null)
    {
        LogId = log.Id;
        ThreadId = log.ThreadId;

        ActivityType = log.ActivityType;
        ActivityTypeDisplay = log.ActivityType.ToDisplayName();

        Details = string.IsNullOrWhiteSpace(log.ActivityDetails) ? "None" : log.ActivityDetails;

        CreatedById = string.IsNullOrEmpty(log.CreatedById) ? "-" : log.CreatedById;
        Username = username ?? "Unknown";

        ActivityDate = log.ActivityTimestampUtc.ToLocalTime().ToString("g");
        RelativeDate = log.ActivityTimestampUtc.ToRelativeTime();
    }
}
