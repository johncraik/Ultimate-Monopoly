using MP.GameEngine.Enums;

namespace MP.GameEngine.Models.Snapshot;

public class EventInfo
{
    /// <summary>
    /// Station rent multiplier. When 0, station rent becomes no-op.
    /// </summary>
    public ushort? StationRentMultiplier { get; set; }
    public bool StationEvent => StationRentMultiplier != null;

    /// <summary>
    /// Multiplier for utility rents. When 0, utility rent becomes no-op.
    /// </summary>
    public ushort? UtilityRentMultiplier { get; set; }
    public bool UtilityEvent => UtilityRentMultiplier != null;

    /// <summary>
    /// Multiplier for tax fees. When 0, tax becomes no-op (except for tax card).
    /// </summary>
    public ushort? TaxMultiplier { get; set; }
    public bool TaxEvent => TaxMultiplier != null;

    /// <summary>
    /// Cancels all actions on free parking space (even FP card),
    /// returning the space to normal monopoly rules
    /// </summary>
    public bool RealFreeParking { get; set; }

    /// <summary>
    /// When true, no player can enter jail and instead pay their jail fee (immediate leave jail by pay)
    /// Activated by a card "Jail at max capacity! When you are sent to jail you must pay your jail fee. This occurs until a double is rolled"
    /// </summary>
    public bool JailFull { get; set; }


    public EventInfo()
    {
    }

    public EventInfo(EventInfo model)
    {
        StationRentMultiplier = model.StationRentMultiplier;
        UtilityRentMultiplier = model.UtilityRentMultiplier;
        TaxMultiplier = model.TaxMultiplier;
        RealFreeParking = model.RealFreeParking;
        JailFull = model.JailFull;
    }

    public EventInfo(GlobalEvent eventType, ushort? multiplier = null)
    {
        switch (eventType)
        {
            case GlobalEvent.StationRent:
                if(multiplier == null)
                    throw new ArgumentNullException(nameof(multiplier));
                    
                StationRentMultiplier = multiplier;
                break;
            case GlobalEvent.UtilityRent:
                if(multiplier == null)
                    throw new ArgumentNullException(nameof(multiplier));
                
                UtilityRentMultiplier = multiplier;
                break;
            case GlobalEvent.TaxMultiplier:
                if(multiplier == null)
                    throw new ArgumentNullException(nameof(multiplier));
                
                TaxMultiplier = multiplier;
                break;
            case GlobalEvent.RealFreeParking:
                RealFreeParking = true;
                break;
            case GlobalEvent.JailFull:
                JailFull = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null);
        }
    }
}