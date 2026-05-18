using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Extensions;
using JC.Core.Models.Auditing;
using UltimateMonopoly.Enums;
using UltimateMonopoly.Helpers;

namespace UltimateMonopoly.Models.DataModels.Boards;

public class CustomBoardSpace : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    
    [Required]
    [MaxLength(38)]
    public string BoardId { get; set; }
    [ForeignKey(nameof(BoardId))]
    public CustomBoard Board { get; set; }
    
    [Required]
    [MaxLength(128)]
    public string Name { get; set; }
    
    public ushort Index { get; private set; }
    public BoardSpaceType SpaceType { get; private set; }
    public PropertyColour? PropertyColour { get; private set; }

    public bool SetSpaceProperties(ushort desiredIndex, BoardSpaceType spaceType)
    {
        var index = IndexHelper.ResolveIndex(desiredIndex, spaceType);
        //Cannot customise card spaces
        if (index == null || index.Value.IsCard()) return false;
        
        var colour = PropertyColourHelper.ResolveColour(index.Value);
        //Prevent setting properties if no colour and is a property:
        if (colour == null && index.Value.IsProperty()) return false;
        
        Index = index.Value;
        SpaceType = spaceType;
        PropertyColour = colour;
        return true;
    }
    

    [NotMapped]
    public bool IsProperty => Index.IsProperty() && PropertyColour != null;
    
    [NotMapped]
    public bool IsCorner => Index.IsCorner();
    
    public override string ToString() => $"{Name} ({SpaceType.ToDisplayName()} - {Index})";
}