namespace UltimateMonopoly.Helpers;

public record RuleSection(string Section, int SectionNumber);

public static class PageRulesHelper
{
    private const string DefaultSection = "Standard Monopoly Rules/Convensions";
    private const string TurnTaxSection = "Player Turn Tax";
    private const string DiceSection = "Dice Rolls";
    private const string MovementSection = "Movement";
    private const string DoubleDiceSection = "Double Dice Rolls";
    private const string TripleDiceSection = "Triple Dice Rolls";
    private const string CardsSection = "Cards";
    private const string GlobalEventsSection = "Global Events";
    private const string GoSection = "GO Space";
    private const string GoToJailSection = "Just Visiting & Go to Jail Spaces";
    private const string JailSection = "Jail";
    private const string FreeParkingSection = "Free Parking";
    private const string AuctionsSection = "Auctions";
    private const string ReservedPropertiesSection = "Reserved Properties";
    private const string BuildingRulesSection = "Building Rules";
    private const string StationsSection = "Stations";
    private const string UtilitiesSection = "Utilities";
    private const string MortgagingSection = "Mortgaging";
    private const string LoansSection = "Loans";
    private const string PurgingSection = "Purging";
    private const string BankruptcySection = "Bankruptcy";

    public static List<RuleSection> GetSections()
        =>
        [
            new(DefaultSection, 0),
            new(TurnTaxSection, 1),
            new(DiceSection, 2),
            new(MovementSection, 3),
            new(DoubleDiceSection, 4),
            new(TripleDiceSection, 5),
            new(CardsSection, 6),
            new(GlobalEventsSection, 7),
            new(GoSection, 8),
            new(GoToJailSection, 9),
            new(JailSection, 10),
            new(FreeParkingSection, 11),
            new(AuctionsSection, 12),
            new(ReservedPropertiesSection, 13),
            new(BuildingRulesSection, 14),
            new(StationsSection, 15),
            new(UtilitiesSection, 16),
            new(MortgagingSection, 17),
            new(LoansSection, 18),
            new(PurgingSection, 19),
            new(BankruptcySection, 20)
        ];
    
    public static RuleSection? GetSection(int sectionNumber)
        => GetSections().FirstOrDefault(x => x.SectionNumber == sectionNumber);
}