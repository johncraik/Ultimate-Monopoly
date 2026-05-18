using UltimateMonopoly.Enums;

namespace UltimateMonopoly.Helpers;

public static class PropertyColourHelper
{
    public static readonly ushort[] BrownPropIndexes = [1, 3];
    public static readonly ushort[] BluePropIndexes = [6, 8, 9];
    public static readonly ushort[] PinkPropIndexes = [11, 13, 14];
    public static readonly ushort[] OrangePropIndexes = [16, 18, 19];
    public static readonly ushort[] RedPropIndexes = [21, 23, 24];
    public static readonly ushort[] YellowPropIndexes = [26, 27, 29];
    public static readonly ushort[] GreenPropIndexes = [31, 32, 34];
    public static readonly ushort[] DarkBluePropIndexes = [37, 39];

    public static PropertyColour? ResolveColour(ushort index)
    {
        if (BrownPropIndexes.Contains(index))
            return PropertyColour.Brown;
        
        if (BluePropIndexes.Contains(index))
            return PropertyColour.Blue;

        if (PinkPropIndexes.Contains(index))
            return PropertyColour.Pink;

        if (OrangePropIndexes.Contains(index))
            return PropertyColour.Orange;

        if (RedPropIndexes.Contains(index))
            return PropertyColour.Red;
        
        if (YellowPropIndexes.Contains(index))
            return PropertyColour.Yellow;
        
        if (GreenPropIndexes.Contains(index))
            return PropertyColour.Green;
        
        if (DarkBluePropIndexes.Contains(index))
            return PropertyColour.DarkBlue;
        
        return null;
    }
}