using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using Microsoft.EntityFrameworkCore;

namespace UltimateMonopoly.Models.DataModels.Social;

[Index(nameof(CreatedById), nameof(BlockedUserId))]
[Index(nameof(BlockedUserId))]
public class BlockedUser : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    
    [NotMapped]
    public string FromUserId => CreatedById ?? throw new InvalidOperationException("From user id not set");
    
    [Required]
    [MaxLength(38)]
    //No nav prop foreign key - allows account deletion
    public string BlockedUserId { get; private set; }

    [NotMapped]
    public DateTime DateBlockedUtc => CreatedUtc;

    public BlockedUser()
    {
    }

    public BlockedUser(string userId)
    {
        BlockedUserId = userId;
    }
}