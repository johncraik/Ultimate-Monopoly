using JC.Core.Extensions;
using UltimateMonopoly.Areas.Admin.Enums;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;

public class AdminLogViewModel
{
    public string LogId { get; }

    public string Username { get; } = "Unknown";
    public string UserId { get; } = "-";
    
    public AdminActionType Action { get; }
    public string ActionDisplay { get; }
    
    public AdminTargetType Target { get; }
    public string TargetDisplay { get; }
    
    public string TargetId { get; }
    public bool? TargetIsAdmin { get; }

    public string RelativeDate { get; }
    public string LogDate { get; }
    
    public string Details { get; }

    public AdminLogViewModel(AdminActionLog log)
    {
        LogId = log.Id;

        Action = log.Action;
        ActionDisplay = log.Action.ToDisplayName();

        Target = log.TargetType;
        TargetDisplay = log.TargetType.ToDisplayName();
        TargetId = log.TargetId ?? "N/A";
        
        RelativeDate = log.CreatedUtc.ToRelativeTime();
        LogDate = log.CreatedUtc.ToLocalTime().ToString("g");
        
        Details = log.Detail ?? "None";
    }

    public AdminLogViewModel(AdminActionLog log, string? username, string? userId, bool targetIsAdmin)
        : this(log)
    {
        Username = username ?? "Unknown";
        UserId = userId ?? "-";
        TargetIsAdmin = targetIsAdmin;
    }
}