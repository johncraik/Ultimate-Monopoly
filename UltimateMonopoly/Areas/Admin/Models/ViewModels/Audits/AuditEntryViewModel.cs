using System.Text.Json;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Auditing;
using Syncfusion.EJ2.Navigations;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;

public class AuditDataTableViewModel
{
    public string TableName { get; }
    public int AuditCount { get; }
    public string LatestAuditDate { get; }
    
    public AuditDataTableViewModel(string tableName, int auditCount, string? latestAuditDate)
    {
        TableName = tableName ?? "Unknown";
        AuditCount = auditCount;
        LatestAuditDate = latestAuditDate ?? "Never";
    }
}

public class AuditEntryViewModel
{
    public string AuditId { get; }
    
    public AuditAction Action { get; }
    public string ActionDisplay { get; }
    
    public string RelativeDate { get; }
    public string AuditDate { get; }
    
    public string UserId { get; }
    public string UserName { get; }
    public string TableName { get; }
    
    public bool IsUserTrail { get; }
    
    public Dictionary<string, object?> TableKeys { get; }
    public string? ActionDataRaw { get; }
    public string? ActionData { get; }

    public AuditEntryViewModel(AuditEntry entry, bool isUserTrail)
    {
        AuditId = entry.Id;
        IsUserTrail = isUserTrail;
        
        Action = entry.Action;
        ActionDisplay = entry.Action.ToDisplayName();

        RelativeDate = entry.AuditDate.ToRelativeTime();
        AuditDate = entry.AuditDate.ToLocalTime().ToString("g");
        
        UserId = entry.UserId switch
        {
            IUserInfo.UNKNOWN_USER_ID or IUserInfo.SYSTEM_USER_ID => "System",
            null or "" => "Unknown",
            _ => entry.UserId
        };
        
        UserName = entry.UserName switch
        {
            IUserInfo.UNKNOWN_USER_NAME or IUserInfo.SYSTEM_USER_NAME => "System",
            null or "" => "Unknown",
            _ => entry.UserName
        };
        
        TableName = entry.TableName ?? "Unknown";
        ActionDataRaw = entry.ActionData;

        TableKeys = !string.IsNullOrEmpty(entry.EntityKey) 
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(entry.EntityKey) ?? [] 
            : [];

        if (string.IsNullOrEmpty(entry.ActionData)) return;
        
        var obj = JsonSerializer.Deserialize<object>(entry.ActionData);
        if (obj is null) return;

        ActionData = JsonSerializer.Serialize(obj, options: new JsonSerializerOptions() { WriteIndented = true });
    }
}