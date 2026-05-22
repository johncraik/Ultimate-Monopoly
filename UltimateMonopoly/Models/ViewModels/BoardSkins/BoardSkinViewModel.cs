using MP.GameEngine.Enums;
using MP.GameEngine.Helpers;
using UltimateMonopoly.Models.DataModels.Boards;

namespace UltimateMonopoly.Models.ViewModels.BoardSkins;

public class BoardSkinViewModel
{
    public string Id { get; }
    public string Name { get; }
    public string? Description { get; }

    public IReadOnlyList<BoardSkinSpaceViewModel> Spaces { get; set; } = [];
    
    public ushort PropertiesCustomised { get; }
    public ushort StationsCustomised { get; }
    public ushort UtilitiesCustomised { get; }
    public ushort CornersCustomised { get; }
    public ushort TaxSpacesCustomised { get; }

    public BoardSkinViewModel(BoardSkin boardSkin)
    {
        Id = boardSkin.Id;
        Name = boardSkin.Name;
        Description = boardSkin.Description;

        if (boardSkin.Spaces == null!)
            return;
        
        Spaces = boardSkin.Spaces.Select(x => new BoardSkinSpaceViewModel(x)).ToList();
            
        PropertiesCustomised = (ushort)boardSkin.Spaces.Count(x => x.SpaceType == BoardSpaceType.Property);
        StationsCustomised = (ushort)boardSkin.Spaces.Count(x => x.SpaceType == BoardSpaceType.Station);
        UtilitiesCustomised = (ushort)boardSkin.Spaces.Count(x => x.SpaceType == BoardSpaceType.Utility);
        CornersCustomised = (ushort)boardSkin.Spaces.Count(x => x.Index.IsCorner());
        TaxSpacesCustomised = (ushort)boardSkin.Spaces.Count(x => x.SpaceType == BoardSpaceType.Tax);
    }
}