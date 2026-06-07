using MP.GameEngine.Enums;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class GlobalEventService
{
    public GlobalEventService()
    {
        
    }

    public void ClearCurrentEvent(Framework.GameEngine engine)
    {
        var diceType = engine.Cache.TurnDiceRoll?.RollType;
        if (diceType == null || diceType != DiceRollType.Double)
            return;
        
        engine.Cache.Game.GlobalEventInfo = new EventInfo();
    }

    
    public void StartStationRentEvent(Framework.GameEngine engine, ushort? multiplier)
        => StartEvent(engine, GlobalEvent.StationRent, multiplier);
    
    public void StartUtilityRentEvent(Framework.GameEngine engine, ushort? multiplier)
        => StartEvent(engine, GlobalEvent.UtilityRent, multiplier);
    
    public void StartTaxMultiplierEvent(Framework.GameEngine engine, ushort? multiplier)
        => StartEvent(engine, GlobalEvent.TaxMultiplier, multiplier);
    
    public void StartRealFreeParkingEvent(Framework.GameEngine engine)
        => StartEvent(engine, GlobalEvent.RealFreeParking, null);
    
    public void StartJailFullEvent(Framework.GameEngine engine)
        => StartEvent(engine, GlobalEvent.JailFull, null);
    
    private void StartEvent(Framework.GameEngine engine, GlobalEvent eventType, ushort? multiplier)
    {
        var eventInfo = new EventInfo(eventType, multiplier);
        engine.Cache.Game.GlobalEventInfo = eventInfo;
    }
}