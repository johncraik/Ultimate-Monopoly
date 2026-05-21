using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Models.DataModels.Boards;

[Index(nameof(UserId))]
public class BoardSkin : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    
    [Required]
    [MaxLength(38)]
    //No nav prop foreign key - allows account deletion
    public string UserId { get; set; }
    
    [Required]
    [MaxLength(128)]
    public string Name { get; set; }
    [MaxLength(10240)]
    public string? Description { get; set; }
    
    public List<BoardSkinSpace> Spaces { get; set; }
    public List<SharedBoardSkin> SharedWith { get; set; }
}