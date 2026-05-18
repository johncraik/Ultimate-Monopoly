using UltimateMonopoly.Enums;
using UltimateMonopoly.Extensions;
using UltimateMonopoly.Helpers;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Models.ImportModels;

namespace UltimateMonopoly.Models;

public class BoardSpace
{
    public string Name { get; }
    
    public ushort Index { get; private set; }
    public BoardSpaceType SpaceType { get; private set; }
    public PropertyColour? PropertyColour { get; private set; }
    
    public ushort? PurchaseCost { get; }
    public bool IsPurchasable => PurchaseCost != null && !Index.IsCorner() && !Index.IsCard() && !Index.IsTax();
    
    public Dictionary<RentLevel, ushort>? Rents { get; private set; }
    public bool IsRentable => Rents != null && !Index.IsCorner() && !Index.IsCard() && !Index.IsTax();
    
    public ushort? BuildCost { get; private set; }
    public bool IsBuildable => BuildCost != null && Index.IsProperty();
    
    public ushort? Tax { get; }
    public bool IsTaxable => Tax != null && Index.IsTax();

    public BoardSpace(BoardSpaceJsonImport import)
    {
        Name = import.Name;
        var result = SetProperties(import);
        if (!result) throw new ArgumentException("Invalid board space import");
        
        if(Index.IsProperty() || Index.IsStation() || Index.IsUtility())
            PurchaseCost = import.PurchaseCost;
        
        if(Index.IsProperty())
            BuildCost = import.BuildCost;
        
        if(Index.IsTax())
            Tax = import.Tax;
    }

    public BoardSpace(CustomBoardSpace customSpace, BoardSpace defaultSpace)
    {
        Name = customSpace.Name;
        //These are already validated in the CustomBoardSpace private setter
        Index = customSpace.Index;
        SpaceType = customSpace.SpaceType;
        PropertyColour = customSpace.PropertyColour;
        
        PurchaseCost = defaultSpace.PurchaseCost;
        Rents = defaultSpace.Rents;
        BuildCost = defaultSpace.BuildCost;
        Tax = defaultSpace.Tax;
    }


    public bool SetProperties(BoardSpaceJsonImport import)
    {
        if (string.IsNullOrEmpty(import.SpaceType)) return false;
        
        var spaceType = import.SpaceType.ParseBoardSpace();
        if (spaceType == null) return false;

        var index = IndexHelper.ResolveIndex(import.Index, (BoardSpaceType)spaceType);
        if (index == null) return false;
        
        var colour = PropertyColourHelper.ResolveColour(index.Value);
        if (colour == null && index.Value.IsProperty()) return false;
        
        Index = index.Value;
        SpaceType = (BoardSpaceType)spaceType;
        PropertyColour = colour;
        return true;
    }


    public bool SetRents(ushort[] rents)
    {
        var rentDict = new Dictionary<RentLevel, ushort>();
        switch (SpaceType)
        {
            case BoardSpaceType.Property:
                if(rents.Length != 8) return false;
                
                rents = rents.OrderBy(r => r).ToArray();
                
                rentDict.TryAdd(RentLevel.SINGLE, rents[0]);
                rentDict.TryAdd(RentLevel.SET, rents[1]);
                rentDict.TryAdd(RentLevel.ONE_HOUSE, rents[2]);
                rentDict.TryAdd(RentLevel.TWO_HOUSES, rents[3]);
                rentDict.TryAdd(RentLevel.THREE_HOUSES, rents[4]);
                rentDict.TryAdd(RentLevel.FOUR_HOUSES, rents[5]);
                rentDict.TryAdd(RentLevel.HOTEL, rents[6]);
                rentDict.TryAdd(RentLevel.DOUBLE_HOTEL, rents[7]);
                break;
            case BoardSpaceType.Station:
                if(rents.Length != 4) return false;
                
                rents = rents.OrderBy(r => r).ToArray();
                
                rentDict.TryAdd(RentLevel.SINGLE, rents[0]);
                rentDict.TryAdd(RentLevel.DOUBLE, rents[1]);
                rentDict.TryAdd(RentLevel.TRIPLE, rents[2]);
                rentDict.TryAdd(RentLevel.QUADRUPLE, rents[3]);
                break;
            case BoardSpaceType.Utility:
                if(rents.Length != 2) return false;
                
                rents = rents.OrderBy(r => r).ToArray();
                
                rentDict.TryAdd(RentLevel.SINGLE, rents[0]);
                rentDict.TryAdd(RentLevel.DOUBLE, rents[1]);
                break;
            case BoardSpaceType.Tax:
            case BoardSpaceType.Chance:
            case BoardSpaceType.ComChest:
            case BoardSpaceType.Go:
            case BoardSpaceType.Jail:
            case BoardSpaceType.JustVisiting:
            case BoardSpaceType.FreeParking:
            case BoardSpaceType.GoToJail:
            default:
                rentDict = null;
                break;
        }
        
        Rents = rentDict;
        return true;
    }
}