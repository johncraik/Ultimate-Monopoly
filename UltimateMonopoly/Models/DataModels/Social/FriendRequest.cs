using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using Microsoft.EntityFrameworkCore;

namespace UltimateMonopoly.Models.DataModels.Social;

[Index(nameof(CreatedById))]
[Index(nameof(ToUserId))]
public class FriendRequest : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    
    [NotMapped]
    public string FromUserId => CreatedById ?? throw new InvalidOperationException("From user id not set");
    
    [Required]
    [MaxLength(38)]
    //No nav prop foreign key - allows account deletion
    public string ToUserId { get; private set; }

    [NotMapped]
    public DateTime SentAtUtc => CreatedUtc;
    
    //Null when neither accepted/declined
    public bool? IsAccepted { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }


    public FriendRequest()
    {
    }

    public FriendRequest(string toUserId)
    {
        ToUserId = toUserId;
    }
    
    public void Accept()
    {
        if(IsAccepted.HasValue)
            return;
        
        IsAccepted = true;
        AcknowledgedAtUtc = DateTime.UtcNow;
    }

    public void Decline()
    {
        if(IsAccepted.HasValue)
            return;
        
        IsAccepted = false;
        AcknowledgedAtUtc = DateTime.UtcNow;
    }
}