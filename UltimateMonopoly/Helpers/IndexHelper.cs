using UltimateMonopoly.Enums;
using UltimateMonopoly.Enums.Players;

namespace UltimateMonopoly.Helpers;

public static class IndexHelper
{
    //Includes virtual Jail space (shares space with Just Visiting)
    public const ushort BoardSize = 41;
    public const ushort PhysicalBoardSize = 40;
    
    public const ushort GoSpace = 0;
    public const ushort JustVisitingSpace = 10;
    public const ushort FreeParkingSpace = 20;
    public const ushort GoToJailSpace = 30;
    public static readonly ushort[] CornerIndexes = [GoSpace, JustVisitingSpace, JailSpace, FreeParkingSpace, GoToJailSpace];
    
    //Jail spaceType is the same spaceType as just visiting spaceType (in physical board)
    //Therefore, index is just visiting spaceType * 10, keeping it a multiple of just visiting spaceType;
    //while also preventing duplicate index values
    public const ushort JailSpace = JustVisitingSpace * 10;

    public const ushort IncomeTaxSpace = 4;
    public const ushort SuperTaxSpace = 38;
    public static readonly ushort[] TaxIndexes = [IncomeTaxSpace, SuperTaxSpace];
    
    public static readonly ushort[] ChanceIndexes = [7, 22, 36];
    public static readonly ushort[] ComChestIndexes = [2, 17, 33];
    public static readonly ushort[] CardIndexes = [..ChanceIndexes, ..ComChestIndexes];
    
    public static readonly ushort[] StationIndexes = [5, 15, 25, 35];
    public static readonly ushort[] UtilityIndexes = [12, 28];
    
    public static readonly ushort[] PropertyIndexes =
    [
        ..PropertyColourHelper.BrownPropIndexes,
        ..PropertyColourHelper.BluePropIndexes,
        ..PropertyColourHelper.PinkPropIndexes,
        ..PropertyColourHelper.OrangePropIndexes,
        ..PropertyColourHelper.RedPropIndexes,
        ..PropertyColourHelper.YellowPropIndexes,
        ..PropertyColourHelper.GreenPropIndexes,
        ..PropertyColourHelper.DarkBluePropIndexes
    ];


    /// <summary>
    /// Resolves the desired index of a specific board spaceType based on its type.
    /// </summary>
    /// <param name="desiredIndex">The index being looked up in the context of the specified board spaceType.</param>
    /// <param name="spaceType">The type of board spaceType to validate and resolve the index against.</param>
    /// <returns>
    /// The resolved index if the specified <paramref name="desiredIndex"/> is valid for the given
    /// <paramref name="spaceType"/> type, otherwise null. For fixed spaces like 'Go' or 'Jail',
    /// their respective indexes are returned directly.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="spaceType"/> does not match a valid board spaceType type.
    /// </exception>
    public static ushort? ResolveIndex(ushort desiredIndex, BoardSpaceType spaceType)
        => spaceType switch
        {
            BoardSpaceType.Property => !PropertyIndexes.Contains(desiredIndex) ? null : desiredIndex,
            BoardSpaceType.Station => !StationIndexes.Contains(desiredIndex) ? null : desiredIndex,
            BoardSpaceType.Utility => !UtilityIndexes.Contains(desiredIndex) ? null : desiredIndex,
            BoardSpaceType.Tax => !TaxIndexes.Contains(desiredIndex) ? null : desiredIndex,
            BoardSpaceType.Chance => !ChanceIndexes.Contains(desiredIndex) ? null : desiredIndex,
            BoardSpaceType.ComChest => !ComChestIndexes.Contains(desiredIndex) ? null : desiredIndex,
            BoardSpaceType.Go => GoSpace,
            BoardSpaceType.Jail => JailSpace,
            BoardSpaceType.JustVisiting => JustVisitingSpace,
            BoardSpaceType.FreeParking => FreeParkingSpace,
            BoardSpaceType.GoToJail => GoToJailSpace,
            _ => throw new ArgumentOutOfRangeException(nameof(spaceType), spaceType, null)
        };

    /// <summary>
    /// Resolves the index corresponding to the "Just Visiting" spaceType when a player leaves jail.
    /// </summary>
    /// <returns>
    /// The index of the "Just Visiting" spaceType, represented by a constant value.
    /// </returns>
    public static ushort LeaveJail() => JustVisitingSpace;

    /// <summary>
    /// Returns the index of the jail spaceType on the board.
    /// </summary>
    /// <returns>
    /// The index representing the jail spaceType, which is a unique value different from other board spaces.
    /// </returns>
    public static ushort GoToJail() => JailSpace;

    
    /// <summary>
    /// Determines whether the specified index corresponds to a property spaceType on the board.
    /// </summary>
    /// <param name="index">The index to check against property spaces.</param>
    /// <returns>
    /// <c>true</c> if the specified <paramref name="index"/> matches a property spaceType; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsProperty(this ushort index) => PropertyIndexes.Contains(index);

    /// <summary>
    /// Determines whether the specified index corresponds to a station spaceType on the board.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <returns>
    /// <c>true</c> if the specified <paramref name="index"/> matches any station spaceType index;
    /// otherwise, <c>false</c>.
    /// </returns>
    public static bool IsStation(this ushort index) => StationIndexes.Contains(index);

    /// <summary>
    /// Checks whether the specified index corresponds to a utility spaceType on the board.
    /// </summary>
    /// <param name="index">The index to be checked against the utility spaceType identifiers.</param>
    /// <returns>
    /// True if the given <paramref name="index"/> matches one of the predefined utility spaceType indexes; otherwise, false.
    /// </returns>
    public static bool IsUtility(this ushort index) => UtilityIndexes.Contains(index);


    /// <summary>
    /// Determines whether the specified index corresponds to a tax spaceType on the game board.
    /// </summary>
    /// <param name="index">The board index to check for being a tax spaceType.</param>
    /// <returns>
    /// True if the specified <paramref name="index"/> is a valid tax spaceType index; otherwise, false.
    /// </returns>
    public static bool IsTax(this ushort index) => TaxIndexes.Contains(index);

    /// <summary>
    /// Determines whether the specified index corresponds to the Income Tax spaceType on the board.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <returns>
    /// True if the specified <paramref name="index"/> is the Income Tax spaceType; otherwise, false.
    /// </returns>
    public static bool IsIncomeTax(this ushort index) => index == IncomeTaxSpace;

    /// <summary>
    /// Determines whether the specified index represents the 'Super Tax' spaceType on the board.
    /// </summary>
    /// <param name="index">The index to evaluate.</param>
    /// <returns>
    /// True if the specified <paramref name="index"/> matches the 'Super Tax' spaceType; otherwise, false.
    /// </returns>
    public static bool IsSuperTax(this ushort index) => index == SuperTaxSpace;

    
    /// <summary>
    /// Determines whether the given index corresponds to a Card spaceType (either Chance or Community Chest).
    /// </summary>
    /// <param name="index">The index to check for classification as a Card spaceType.</param>
    /// <returns>
    /// <c>true</c> if the specified <paramref name="index"/> matches a Card spaceType index; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsCard(this ushort index) => CardIndexes.Contains(index);

    /// <summary>
    /// Determines whether the specified index corresponds to a Chance spaceType on the board.
    /// </summary>
    /// <param name="index">The board index to evaluate.</param>
    /// <returns>
    /// true if the <paramref name="index"/> represents a Chance spaceType; otherwise, false.
    /// </returns>
    public static bool IsChance(this ushort index) => ChanceIndexes.Contains(index);

    /// <summary>
    /// Determines whether the specified index corresponds to a Community Chest spaceType on the board.
    /// </summary>
    /// <param name="index">The board index to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the specified <paramref name="index"/> is associated with a Community Chest spaceType; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsComChest(this ushort index) => ComChestIndexes.Contains(index);


    /// <summary>
    /// Determines if the specified index corresponds to a corner spaceType on the board.
    /// </summary>
    /// <param name="index">The index to evaluate for being a corner spaceType.</param>
    /// <returns>
    /// True if the specified <paramref name="index"/> matches one of the predefined corner spaces
    /// (e.g., Go, Just Visiting, Free Parking, or Go To Jail); otherwise, false.
    /// </returns>
    public static bool IsCorner(this ushort index) => CornerIndexes.Contains(index);

    /// <summary>
    /// Determines whether the specified index corresponds to the "Go" spaceType on the board.
    /// </summary>
    /// <param name="index">The index to evaluate.</param>
    /// <returns>
    /// True if the specified <paramref name="index"/> corresponds to the "Go" spaceType; otherwise, false.
    /// </returns>
    public static bool IsGo(this ushort index) => index == GoSpace;

    /// <summary>
    /// Determines whether the specified index corresponds to the "Just Visiting" spaceType on the board.
    /// </summary>
    /// <param name="index">The board index to be checked.</param>
    /// <returns>
    /// True if the provided <paramref name="index"/> matches the "Just Visiting" spaceType; otherwise, false.
    /// </returns>
    public static bool IsJustVisiting(this ushort index) => index == JustVisitingSpace;

    /// <summary>
    /// Determines whether the specified index corresponds to the Jail spaceType on the board.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <returns>
    /// <c>true</c> if the specified <paramref name="index"/> matches the Jail spaceType; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsJail(this ushort index) => index == JailSpace;

    /// <summary>
    /// Determines whether the specified index corresponds to the "Free Parking" spaceType.
    /// </summary>
    /// <param name="index">The board index to evaluate.</param>
    /// <returns>
    /// true if the specified <paramref name="index"/> is the "Free Parking" spaceType; otherwise, false.
    /// </returns>
    public static bool IsFreeParking(this ushort index) => index == FreeParkingSpace;

    /// <summary>
    /// Determines whether the given index corresponds to the "Go to Jail" spaceType on the board.
    /// </summary>
    /// <param name="index">The board spaceType index to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the provided <paramref name="index"/> matches the "Go to Jail" spaceType; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsGoToJail(this ushort index) => index == GoToJailSpace;



    public static (ushort Index, ushort GoPasses) MoveIndex(ushort index, ushort spaces, PlayerDirection direction)
    {
        var desiredIndex = (direction switch
        {
            PlayerDirection.Forward => index + spaces,
            PlayerDirection.Backward => index - spaces,
            _ => throw new ArgumentOutOfRangeException()
        });
        
        if (desiredIndex is JailSpace or >= 0 and < PhysicalBoardSize)
            return ((ushort)desiredIndex, 0);

        ushort goPasses = 0;
        while (desiredIndex is < 0 or >= PhysicalBoardSize)
        {
            desiredIndex = direction switch
            {
                PlayerDirection.Forward => desiredIndex - PhysicalBoardSize,
                PlayerDirection.Backward => desiredIndex + PhysicalBoardSize,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
            goPasses++;
        }
        
        return ((ushort)desiredIndex, goPasses);
    }

    public static (ushort Index, bool PassesGo) AdvanceIndex(ushort currentIndex, ushort desiredIndex, PlayerDirection direction)
    {
        if(desiredIndex > PhysicalBoardSize && desiredIndex != JailSpace)
            throw new ArgumentOutOfRangeException(nameof(desiredIndex), "Desired index cannot exceed physical board size.");
        
        var passesGo = direction switch
        {
            PlayerDirection.Forward => desiredIndex <= currentIndex,
            PlayerDirection.Backward => desiredIndex >= currentIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
        return (desiredIndex, passesGo);
    }
}
