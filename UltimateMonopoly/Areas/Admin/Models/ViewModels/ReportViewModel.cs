using JC.Core.Extensions;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels.Social;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels;

public class ReportViewModel
{
    public string ReportedUserId { get; }
    public UserViewModel? ReportedUser { get; }
    
    public string ReportedByUserId { get; }
    public UserViewModel? ReportedByUser { get; }
    
    public string ReportId { get; }
    
    public ReportReason Reason { get; }
    public string ReasonDisplay { get; }
    
    public string Message { get; set; }
    
    public string ReportedDate { get; }
    
    public ReportResolution Resolution { get; }
    public string ResolutionDisplay { get; }

    public ReportViewModel(UserViewModel? reportedUser, UserViewModel? reportedByUser, ReportedUser report)
    {
        if(reportedUser != null)
            ReportedUser = reportedUser;

        if(reportedByUser != null)
            ReportedByUser = reportedByUser;
        
        ReportedUserId = report.BlockedUser?.BlockedUserId ?? "-";
        ReportedByUserId = report.BlockedUser?.FromUserId ?? "-";
        ReportId = report.BlockedId;
        
        Reason = report.Reason;
        ReasonDisplay = report.Reason.ToDisplayName();
        
        Message = report.Message ?? "Not Provided";
        ReportedDate = report.CreatedUtc.ToLocalTime().ToString("g");
        
        Resolution = report.Resolution;
        ResolutionDisplay = report.Resolution.ToDisplayName();
    }
}