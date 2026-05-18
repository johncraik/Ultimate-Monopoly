using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;

namespace UltimateMonopoly.Models.DataModels.Social;

public class ReportedUser : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string BlockedUserId { get; set; }
    [ForeignKey(nameof(BlockedUserId))]
    public BlockedUser BlockedUser { get; set; }
    
    public ReportReason Reason { get; set; }
    [MaxLength(10240)]
    public string? Message { get; set; }
}

public enum ReportReason
{
    //Fill out
}