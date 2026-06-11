using JC.Core.Extensions;

namespace MP.GameEngine.Enums.Cards;

public enum CardType
{
    Chance,
    ComChest,
    CommunityChest = ComChest,
    PercentageChance,
    PercentageComChest,
    PercentageCommunityChest = PercentageComChest,
    Third,
    Double,
    Triple,
    Tax,
    Go,
    JustVisiting,
    FreeParking,
    GoToJail
}

public static class CardTypeExtensions
{
    /// <summary>
    /// Converts a CardType enum value to its corresponding display name.
    /// </summary>
    /// <param name="cardType">The CardType enum value to convert.</param>
    /// <returns>A string representing the display name of the specified CardType.</returns>
    public static string ToDisplayName(this CardType cardType)
        => cardType switch
        {
            //Always returns 'ComChest' as 'Community Chest'
            CardType.ComChest => "Community Chest",
            CardType.PercentageComChest => "Percentage Community Chest",
            //Converts 'Go' to 'GO'
            CardType.Go => nameof(CardType.Go).ToUpperInvariant(),
            //All other cases use the standard EnumExtensions.ToDisplayName method
            _ => EnumExtensions.ToDisplayName(cardType)
        };
}