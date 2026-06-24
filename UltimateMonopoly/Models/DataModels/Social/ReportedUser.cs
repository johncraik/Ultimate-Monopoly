using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using UltimateMonopoly.Areas.Admin.Enums;

namespace UltimateMonopoly.Models.DataModels.Social;

public class ReportedUser : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string BlockedId { get; set; }
    [ForeignKey(nameof(BlockedId))]
    public BlockedUser BlockedUser { get; set; }

    public ReportReason Reason { get; set; }
    [MaxLength(10240)]
    public string? Message { get; set; }

    public ReportResolution Resolution { get; set; } = ReportResolution.Open;

    public ReportedUser()
    {
    }

    public ReportedUser(string blockedId, ReportInput report)
    {
        BlockedId = blockedId;
        Reason = report.Reason;
        Message = report.message;
    }
}

public record ReportInput(ReportReason Reason, string? message);

public enum ReportReason
{
    [Description("Harassment or bullying")]
    Harassment,

    [Description("Hate speech or discrimination")]
    HateSpeech,

    [Description("Threats or violence")]
    Threats,

    [Description("Inappropriate or explicit content")]
    InappropriateContent,

    [Description("Self-harm or suicide content")]
    SelfHarm,

    [Description("Spam or unwanted messages")]
    Spam,

    [Description("Impersonation")]
    Impersonation,

    [Description("Other")]
    Other
}