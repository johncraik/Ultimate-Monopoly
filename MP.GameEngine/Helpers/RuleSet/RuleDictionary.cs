namespace MP.GameEngine.Helpers.RuleSet;

public static class RuleDictionary
{
    //Game Setup / Metadata
    public const string GameName = "Monappoly: Property Pandemonium";
    public const string Currency = "£";
    public const ushort MinimumPlayers = 2;
    public const ushort MaximumPlayers = 6;
    public const ushort StartingMoney = 1500;
    
    //Jail Consts
    public const ushort DefaultJailCost = 50;
    public const float JailCostMultiplier = 0.5f;
    public const ushort MaxJailTurns = 3;
    public const ushort DoublesBeforeJail = 2;
    public const ushort TriplesBeforeJail = 2;
    
    //Triple Bonus
    public const ushort DefaultTripleBonus = 1000;
    public const ushort TripleBonusIncrease = 500;

    //Dice bonuses
    public const ushort SnakeEyesBonus = 500;
    public const ushort DiceNumRolledBonus = 100;
    
    //GO Bonuses
    public const ushort GoPassClockwiseBonus = 200;
    public const ushort GoPassCounterClockwiseBonus = 100;
    public const ushort LandOnGoBonus = 200;
    
    //Free Parking
    public const ushort FPPayMultiplier = 100;
    public const ushort NormalFPCap = 1000;
    public const ushort DHotelFPCap = 2000;
    
    //Station Rules
    public const ushort SingleStationCost = 200;
    public const ushort SecondStationCost = SingleStationCost + 50; //2 stations grants rent of 50, so increase by 50
    public const ushort ThirdStationCost = SingleStationCost + 100; //3 stations grants rent of 100, so increase by 100
    public const ushort FourthStationCost = SingleStationCost + 200; //4 stations grants rent of 200, so increase by 200
    
    //Houses and Hotels
    public const ushort HouseCount = 35;
    public const ushort HotelCount = 13;
    public const ushort MaxDoubleHotelsPerSet = 1;
    
    //Loans
    public const ushort MaxLoans = 3;
    public const ushort BalanceToKeep = 200;    //This is the amount the player is allowed to keep back before taking a loan
    public const float LoanRepayment = 0.1f;
    
    //Mortgage Fee
    public const float MortgageFee = 0.2f;
}