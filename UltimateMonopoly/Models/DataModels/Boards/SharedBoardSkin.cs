using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using Microsoft.EntityFrameworkCore;

namespace UltimateMonopoly.Models.DataModels.Boards;

[PrimaryKey(nameof(BoardSkinId), nameof(UserId))]
[Index(nameof(UserId))]
public class SharedBoardSkin : AuditModel
{
    [Required]
    [MaxLength(38)]
    public string BoardSkinId { get; private set; }
    [ForeignKey(nameof(BoardSkinId))]
    public BoardSkin BoardSkin { get; set; }
    
    [Required]
    [MaxLength(38)]
    public string UserId { get; private set; }

    public SharedBoardSkin()
    {
    }

    public SharedBoardSkin(string skinId, string userId)
    {
        BoardSkinId = skinId;
        UserId = userId;
    }
}