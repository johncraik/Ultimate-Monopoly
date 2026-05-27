namespace MP.GameEngine.Helpers.RuleSet;

public static class RuleDictionary
{
    public const string GameName = "Monopoly: Property Pandamonium";
    public const string Currency = "£";
    public const ushort MinimumPlayers = 2;
    public const ushort MaximumPlayers = 6;
    
    public const ushort StartingMoney = 1500;
    
    public const ushort DefaultJailCost = 50;
    public const float JailCostMultiplier = 0.5f;
    
    public const ushort DefaultTripleBonus = 1500;
    public const ushort TripleBonusIncrease = 500;

    public const ushort SnakeEyesBonus = 500;
    public const ushort DoublesBeforeJail = 2;
    public const ushort TriplesBeforeJail = 2;
    
    public const ushort DiceNumRolledBonus = 100;
}