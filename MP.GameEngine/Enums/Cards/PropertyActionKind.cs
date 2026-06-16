namespace MP.GameEngine.Enums.Cards;

/// <summary>What a <c>PropertyAction</c> does with title(s).</summary>
public enum PropertyActionKind
{
    /// <summary>Return a chosen property (or set) to the bank.</summary>
    ReturnToBank,
    /// <summary>Hand a chosen property (or set) into the Free Parking pot (not recorded against hand-in history).</summary>
    HandInToFreeParking,
    /// <summary>Take a chosen available (bank-owned) property — "choose any available property from the bank".</summary>
    TakeFromBank,
    /// <summary>Receive every property currently held in the Free Parking pot.</summary>
    ReceiveAllFreeParking,
    /// <summary>Return every property (and the money) in the Free Parking pot to the bank.</summary>
    ClearFreeParkingToBank,
    /// <summary>Swap one of the holder's complete sets for a chosen player's complete set, then purge both swapped sets.</summary>
    SwapSet
}
