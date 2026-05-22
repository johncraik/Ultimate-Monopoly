using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services;

public class PropertyService
{
    public PropertyService()
    {
        
    }

    public List<PropertyModel> GetProperties(Board board)
        => board.Spaces
            .Where(s => s.PropertySet != null)
            .Select(s => new PropertyModel
            {
                Name = s.Name,
                BoardIndex = s.Index,
                
                //Explicit defaults:
                OwnerPlayerId = null,
                State = PropertyState.NotOwned,
                RentLevel = RentLevel.SINGLE,
                HasBeenBuiltOn = false
            }).ToList();
}