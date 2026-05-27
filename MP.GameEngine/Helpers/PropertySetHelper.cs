
using MP.GameEngine.Enums.Properties;
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
}