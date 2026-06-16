namespace MP.GameEngine.Models.Cards;

public class SuppressDefault
{
    public bool SuppressGoBonus { get; set; }
    
    public bool SuppressTaxPayment { get; set; }
    
    public bool SuppressFreeParkingFine { get; set; }
    public bool SuppressFreeParkingMoneyTake { get; set; }
    public bool SuppressFreeParkingPropertyTake { get; set; }
    public bool SuppressFreeParkingPropertyHandIn { get; set; }
    public bool SuppressFreeParkingPurge { get; set; }
    public bool SuppressAllFreeParking => SuppressFreeParkingFine 
                                          && SuppressFreeParkingMoneyTake 
                                          && SuppressFreeParkingPropertyTake 
                                          && SuppressFreeParkingPropertyHandIn 
                                          && SuppressFreeParkingPurge;
    
    public bool SuppressGoToJail { get; set; }
    
    public bool SuppressDirectionChange { get; set; }
    
    public bool SuppressTripleBonus { get; set; }

    /// <summary>
    /// Parameterless constructor for JSON deserialisation — the card import sets the individual
    /// suppress-flag booleans directly (System.Text.Json prefers this ctor; the flags-enum ctor below
    /// is for engine code). Without it the type is not deserialisable (the enum-ctor parameter binds to
    /// no property), and any card carrying a <c>SuppressDefault</c> throws on import.
    /// </summary>
    public SuppressDefault()
    {
    }

    public SuppressDefault(SuppressDefaultType suppressDefaultType)
    {
        if(suppressDefaultType == SuppressDefaultType.None)
            return;
        
        SuppressGoBonus = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressGoBonus);
        
        SuppressTaxPayment = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressTaxPayment);
        
        SuppressFreeParkingFine = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressFreeParkingFine);
        SuppressFreeParkingMoneyTake = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressFreeParkingMoneyTake);
        SuppressFreeParkingPropertyTake = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressFreeParkingPropertyTake);
        SuppressFreeParkingPropertyHandIn = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressFreeParkingPropertyHandIn);
        SuppressFreeParkingPurge = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressFreeParkingPurge);
        
        SuppressGoToJail = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressGoToJail);
        
        SuppressDirectionChange = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressDirectionChange);
        
        SuppressTripleBonus = suppressDefaultType.HasFlag(SuppressDefaultType.SuppressTripleBonus);
    }
}

[Flags]
public enum SuppressDefaultType
{
    None = 0,
    SuppressGoBonus = 1,
    SuppressTaxPayment = 2,
    SuppressFreeParkingFine = 4,
    SuppressFreeParkingMoneyTake = 8,
    SuppressFreeParkingPropertyTake = 16,
    SuppressFreeParkingPropertyHandIn = 32,
    SuppressFreeParkingPurge = 64,
    SuppressGoToJail = 128,
    SuppressDirectionChange = 256,
    SuppressTripleBonus = 512,
}