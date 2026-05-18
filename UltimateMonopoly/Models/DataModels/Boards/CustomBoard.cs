using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Models.DataModels.Boards;

public class CustomBoard : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    
    [Required]
    [MaxLength(38)]
    public string UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public AppUser User { get; set; }
    
    [Required]
    [MaxLength(128)]
    public string Name { get; set; }
    [MaxLength(10240)]
    public string? Description { get; set; }
    
    public List<CustomBoardSpace> Spaces { get; set; }
}