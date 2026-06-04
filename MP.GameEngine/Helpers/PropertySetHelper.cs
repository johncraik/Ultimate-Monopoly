
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Helpers;

public static class PropertySetHelper
{
    public static readonly ushort[] BrownPropIndexes = [1, 3];
    public static readonly ushort[] BluePropIndexes = [6, 8, 9];
    public static readonly ushort[] PinkPropIndexes = [11, 13, 14];
    public static readonly ushort[] OrangePropIndexes = [16, 18, 19];
    public static readonly ushort[] RedPropIndexes = [21, 23, 24];
    public static readonly ushort[] YellowPropIndexes = [26, 27, 29];
    public static readonly ushort[] GreenPropIndexes = [31, 32, 34];
    public static readonly ushort[] DarkBluePropIndexes = [37, 39];
    
    public static readonly ushort[] StationIndexes = [5, 15, 25, 35];
    public static readonly ushort[] UtilityIndexes = [12, 28];


    public static readonly Dictionary<PropertySet, PropertySet> StreetPartner = new()
    {
        [PropertySet.Brown] = PropertySet.Blue,
        [PropertySet.Blue] = PropertySet.Brown,
        [PropertySet.Pink] = PropertySet.Orange,
        [PropertySet.Orange] = PropertySet.Pink,
        [PropertySet.Red] = PropertySet.Yellow,
        [PropertySet.Yellow] = PropertySet.Red,
        [PropertySet.Green] = PropertySet.DarkBlue,
        [PropertySet.DarkBlue] = PropertySet.Green
    };

    public static PropertySet? ResolveSet(ushort index)
    {
        if (BrownPropIndexes.Contains(index))
            return PropertySet.Brown;
        
        if (BluePropIndexes.Contains(index))
            return PropertySet.Blue;

        if (PinkPropIndexes.Contains(index))
            return PropertySet.Pink;

        if (OrangePropIndexes.Contains(index))
            return PropertySet.Orange;

        if (RedPropIndexes.Contains(index))
            return PropertySet.Red;
        
        if (YellowPropIndexes.Contains(index))
            return PropertySet.Yellow;
        
        if (GreenPropIndexes.Contains(index))
            return PropertySet.Green;
        
        if (DarkBluePropIndexes.Contains(index))
            return PropertySet.DarkBlue;
        
        if(StationIndexes.Contains(index))
            return PropertySet.Station;
        
        if(UtilityIndexes.Contains(index))
            return PropertySet.Utility;
        
        return null;
    }

    public static List<ushort> GetIndexes(PropertySet set)
        => set switch
        {
            PropertySet.Brown => BrownPropIndexes.ToList(),
            PropertySet.Blue => BluePropIndexes.ToList(),
            PropertySet.Pink => PinkPropIndexes.ToList(),
            PropertySet.Orange => OrangePropIndexes.ToList(),
            PropertySet.Red => RedPropIndexes.ToList(),
            PropertySet.Yellow => YellowPropIndexes.ToList(),
            PropertySet.Green => GreenPropIndexes.ToList(),
            PropertySet.DarkBlue => DarkBluePropIndexes.ToList(),
            PropertySet.Station => StationIndexes.ToList(),
            PropertySet.Utility => UtilityIndexes.ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(set), set, null)
        };

    /// <summary>
    /// Returns the sets the player holds <i>completely</i> from the supplied
    /// property list. The caller controls which properties count by pre-filtering
    /// on state before calling (owned-only, owned-or-mortgaged, reserved-only,
    /// etc.) — this helper does not inspect <see cref="PropertyState"/>. Stations
    /// and utilities are excluded unless <paramref name="onlyBuildable"/> is false.
    /// </summary>
    public static List<PropertySet> GetOwnedSets(string playerId,
        List<PropertyModel> properties,
        bool onlyBuildable = true)
        => properties
            .Where(p => p.OwnerPlayerId == playerId)
            .GroupBy(p => ResolveSet(p.BoardIndex))
            .Where(g => g.Key is { } set
                        && (!onlyBuildable || set is not (PropertySet.Station or PropertySet.Utility)))
            .Where(g => g.Count() == GetIndexes((PropertySet)g.Key!).Count)
            .Select(g => (PropertySet)g.Key!)
            .ToList();

    public static bool MustReserve(PropertySet set, List<PropertyModel> ownedPropertiesInSet)
    {
        var propIndexes = ownedPropertiesInSet.Select(p => p.BoardIndex).ToList();
        return MustReserve(set, propIndexes);
    }

    public static bool MustReserve(PropertySet set, List<ushort> ownedPropertiesInSet)
        => set is not (PropertySet.Station or PropertySet.Utility)
           && GetIndexes(set).Count - 1 == ownedPropertiesInSet.Count;


    public static uint GetBuildCost(ushort boardIndex, Board board)
    {
        var space = board.GetBoardSpace(boardIndex);
        if(!space.IsBuildable || space.BuildCost == null)
            return 0;

        return (uint)space.BuildCost;
    }

    public static uint GetBuildCost(PropertySet set, Board board)
    {
        var indexes = GetIndexes(set);
        return indexes.Aggregate<ushort, uint>(0, (current, i) => current + GetBuildCost(i, board));
    }

    public static uint GetDoubleHotelCost(ushort boardIndex, Board board)
    {
        var buildCost = GetBuildCost(boardIndex, board);
        
        //Double hotel cost is the total cost of a hotel
        //Build cost = £100: you pay £100 FIVE times to reach hotel level
        //Therefore, double hotel cost = build cost * 5
        return buildCost * 5;
    }
    
    public static uint GetDoubleHotelCost(PropertySet set, Board board)
    {
        var indexes = GetIndexes(set);
        return GetDoubleHotelCost(indexes[0], board);
    }

    
    private static uint Half(uint value)
        => (uint)Math.Round((value / 2d), MidpointRounding.AwayFromZero);
    
    public static uint GetSellValue(ushort boardIndex, Board board)
    {
        var cost = GetBuildCost(boardIndex, board);
        return Half(cost);
    }
    
    public static uint GetSellValue(PropertySet set, Board board)
    {
        var indexes = GetIndexes(set);
        return indexes.Aggregate<ushort, uint>(0, (current, i) => current + GetSellValue(i, board));
    }

    public static uint GetDoubleHotelSellValue(ushort boardIndex, Board board)
    {
        var cost = GetDoubleHotelCost(boardIndex, board);
        return Half(cost);
    }
    
    public static uint GetDoubleHotelSellValue(PropertySet set, Board board)
    {
        var indexes = GetIndexes(set);
        return GetDoubleHotelSellValue(indexes[0], board);
    }
}