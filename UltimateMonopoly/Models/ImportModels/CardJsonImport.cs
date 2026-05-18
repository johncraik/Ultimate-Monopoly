namespace UltimateMonopoly.Models.ImportModels;

public class CardJsonImport
{
    public string CardType { get; set; }
    public string RawText { get; set; }

    public CardActionJson[] Actions { get; set; } = [];
    public bool KeepUntilNeeded { get; set; }
    
    //TODO - Decide on whether card imports should be dependant on its actions, or if all cards should be flat JSON import; with nullable fields
    //Decided to make it array of actions (since a card can be multi-action / decision based), where each action is flat JSON
}

public class CardActionJson
{
    //Groupings wrap brackets around actions during logical evaluation
    //E.g.: A=Action: A1:{1,null}, A2:{2,OR}, A3:{2,AND}, A4:{3,OR} -> reads as (A1 OR (A2 AND A3)) OR A4
    //ID value has no meaning, other than equal values implies same group
    public ushort GroupId { get; set; }
    //Operator comes BEFORE the action
    //E.g.: A=Action: A1:{null}, A2:{OR} -> reads as A1 OR A2
    public string? Operator { get; set; }
    
    //How many times does this action occur
    //E.g.: "Do not collect money from Go for the next 5 times" -> 5 occasions
    public ushort OccasionCount { get; set; } = 1;
    
    //Money value in action
    //Negative values are "pay" actions, positive values are "receive" actions
    public int? Value { get; set; }
    public bool HasValue => Value != null;
    
    //Movement action (e.g. "Advance 2 spaces")
    //NOTE: Movement = move relative to current position, not absolute index
    public CardMovement? Movement { get; set; }
    public bool HasMovement => Movement != null;
    
    //Advance action (e.g. "Advance to Mayfair")
    public CardAdvance? Advance { get; set; }
    public bool HasAdvance => Advance != null;
    
    //Taking/Removing (from player perspective)
    //Bank > Free Parking > Player -- if all null, bank is assumed
    public bool? TakeProperty { get; set; }
    public bool? PropertyBank { get; set; }
    public bool? PropertyFreeParking { get; set; }
    public bool? PropertyPlayer { get; set; }
    public ushort? PropertyCount { get; set; }
    public bool HasProperty => PropertyCount != null && TakeProperty != null;
    
    //Purging (from player perspective) properties
    //E.g. "Purge 2 of your properties" or "Purge another players property"
    public bool? PurgeYourProperties { get; set; }
    public ushort? PurgeCount { get; set; }
    public bool HasPurge => PurgeCount != null && PurgeYourProperties != null;
    
    //GO space actions:
    //Ignored if all null
    //Collect > Clockwise Bonus > Counter Clockwise Bonus > Dice Multiplier
    public bool CollectLandOnGoBonus { get; set; } = true;
    public ushort? ClockwiseGoBonus { get; set; }
    public ushort? CounterClockwiseGoBonus { get; set; }
    public ushort? DiceMultiplier { get; set; }
    public bool HasGoAction => !CollectLandOnGoBonus || ClockwiseGoBonus != null || CounterClockwiseGoBonus != null || DiceMultiplier != null;
    
    //Jail actions:
    public bool? SkipJail { get; set; }
    public bool HasJailAction => SkipJail != null;
    
    //Free Parking actions:
    public bool? StealFreeParking { get; set; }
    public bool? CollectAllMoney { get; set; }
    public ushort? TakeFromFreeParking { get; set; }
    public bool HasFreeParkingAction => StealFreeParking != null || CollectAllMoney != null || TakeFromFreeParking != null;
    
    //Global Events:
    public bool? GlobalEvent { get; set; }
    public string? SpaceTypeEvent { get; set; }
    public ushort? RentMultiplier { get; set; }
    public bool HasGlobalEvent => GlobalEvent != null && SpaceTypeEvent != null && RentMultiplier != null;
}

public record CardMovement(ushort spaces, bool clockwise);

public record CardAdvance(ushort index, bool clockwise);