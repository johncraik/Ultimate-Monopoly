using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;

namespace UltimateMonopoly.Models.DataModels.Social;

public class Friend : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    
    [NotMapped]
    public string AcceptedByUserId => CreatedById ?? throw new InvalidOperationException("User id not set");
    
    [Required]
    [MaxLength(38)]
    public string FriendUserId { get; private set; }
    
    [NotMapped]
    public DateTime DateAddedUtc => CreatedUtc;
    public DateTime? DateRemovedUtc {get; private set; }

    public void Add(string friendUserId)
    {
        if(string.IsNullOrWhiteSpace(FriendUserId))
           FriendUserId = friendUserId; 
    }
    
    public void Remove()
    {
        DateRemovedUtc ??= DateTime.UtcNow;
    }
}