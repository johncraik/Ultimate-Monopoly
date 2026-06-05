namespace MP.GameEngine.Enums;

public enum PropertyTransferReason
{
    /// <summary>When a player buys a property outright from the bank.</summary>
    Buy,
    
    /// <summary>When a player reserves a property; they still own the property, just in a reserved state from the bank.</summary>
    Reserved,
    
    /// <summary>When a player wins a property in an auction.</summary>
    Auction,
    
    /// <summary>When a player gives or receives a property during a deal.</summary>
    Deal,
    
    /// <summary>When a player takes or hands in a property from free parking.</summary>
    FreeParking,
    
    /// <summary>When a player returns a property to the bank, either from a card or cancelled reservation.</summary>
    ReturnToBank,
    
    /// <summary>When a player bankrupts and all their owned properties are returned to the bank.</summary>
    Bankrupt
}