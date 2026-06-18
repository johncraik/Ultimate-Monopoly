namespace MP.GameEngine.Enums.Cards;

/// <summary>What a <c>JailAction</c> does.</summary>
public enum JailKind
{
    SendToJail,
    /// <summary>Get out of jail (free) / release from jail.</summary>
    Release,
    /// <summary>Modify the player's cost to leave jail — set it to a fixed amount or multiply it.</summary>
    ModifyLeaveFee,
    /// <summary>Swap the holder's jail-leave cost with the context player's (a swap partner an earlier action stashed) — "swap places with a jailed player, fees also swapped".</summary>
    SwapLeaveFee
}