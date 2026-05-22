using MP.GameEngine.Enums.Properties;

namespace MP.GameEngine.Models.Snapshot;

public class PropertyModel
{
    public string Name { get; set; }
    public ushort BoardIndex { get; set; }
    
    public string? OwnerPlayerId { get; set; }
    public PropertyState State { get; set; }
    
    public RentLevel RentLevel { get; set; }
    
    /// <summary>
    /// True if the current owner has built this property.
    /// Resets to false whenever owner changes.
    /// Used for owning the street rule.
    /// </summary>
    public bool HasBeenBuiltOn { get; set; }
}