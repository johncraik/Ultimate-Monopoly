using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;

namespace MP.GameEngine.Models.Statistics;

public class PlayerStatRecord : AuditModel
{
    //13.1 - Headline Cash Flow:
    #region 13.1 - Headline Cash Flow:

    /// <summary>Sum of the amount earned each turn (amount &gt; 0)</summary>
    public uint MoneyEarned { get; set; }
    
    /// <summary>Sum of the amount spent each turn (amount &lt; 0)</summary>
    public uint MoneySpent { get; set; }
    
    /// <summary>Sum of amount earned each turn minus the sum of the amount spent each turn</summary>
    [NotMapped]
    public long NetCashFlow => (long)MoneyEarned - MoneySpent;
    
    
    /// <summary>The value of the largest single payment in the game</summary>
    public uint LargestSinglePayment { get; set; }
    
    /// <summary>The financial reason for the largest single payment in the game</summary>
    public FinancialReason? LargestSinglePaymentReason { get; set; }
    
    /// <summary>
    /// The board index of the property for the largest single payment in the game.
    /// Null if the largest payment was not buy/unmortgage/unreserve/rent.
    /// </summary>
    public ushort? LargestSinglePaymentPropertyIndex { get; set; }
    
    
    /// <summary>
    /// The value of the largest rent payment in the game.
    /// Null when no rent payments were made.
    /// </summary>
    public uint? LargestRentPayment { get; set; }
    
    /// <summary>
    /// The board index of the property for the largest rent payment in the game.
    /// Null if the largest rent payment was not made.
    /// </summary>
    public ushort? LargestRentPaymentPropertyIndex { get; set; }

    #endregion


    //13.2 - Spending breakdown
    #region 13.2 - Spending Breakdown

    /// <summary>Sum of the amount of money spent acquiring (buy/auction/reserve) properties</summary>
    public uint SpentAcquiringProperty { get; set; }
    
    /// <summary>Sum of the amount of money spent building houses/hotels on properties</summary>
    public uint SpentBuilding { get; set; }
    
    /// <summary>Sum of the amount of money spent unmortgaging properties</summary>
    public uint SpentUnmortgaging { get; set; }
    
    /// <summary>Sum of the amount of money spent paying fines</summary>
    public uint SpentOnFines { get; set; }
    
    /// <summary>Sum of the amount of money spent leaving jail</summary>
    public uint SpentOnLeavingJail { get; set; }
    
    /// <summary>Sum of the amount of money spent on loan repayments</summary>
    public uint SpentOnRepayingLoans { get; set; }
    
    /// <summary>Sum of the amount of money spent on rent payments</summary>
    public uint RentPaid { get; set; }
    
    /// <summary>Sum of the amount of money given in deals</summary>
    public uint MoneyGivenInDeals { get; set; }
    
    #endregion
    
    
    //13.3 - Income breakdown
    #region 13.3 - Income Breakdown
    
    /// <summary>Sum of the amount of money earned from rent</summary>
    public uint RentEarned { get; set; }
    
    
    /// <summary>Number of times passed GO</summary>
    public uint TimesPassedGo { get; set; }
    
    /// <summary>Sum of the amount of money collected from GO (passing or landing)</summary>
    public uint MoneyCollectedFromGo { get; set; }
    
    
    /// <summary>Sum of the amount of money collected from selling houses/hotels on properties</summary>
    public uint MoneyFromSelling { get; set; }
    
    /// <summary>Sum of the amount of money earned from card payouts</summary>
    public uint MoneyFromCards { get; set; }
    
    /// <summary>Sum of the amount of money earned from mortgaging properties</summary>
    public uint MoneyFromMortgaging { get; set; }
    
    /// <summary>Sum of the amount of money earned from taking out of free parking</summary>
    public uint MoneyFromFreeParking { get; set; }
    
    
    /// <summary>Sum of the amount of money earned from gaining triple bonuses</summary>
    public uint MoneyFromTriples { get; set; }
    
    /// <summary>Sum of the amount of money earned from rolling snake eyes</summary>
    public uint MoneyFromSnakeEyes { get; set; }
    
    /// <summary>Sum of the amount of money earned from their dice number being rolled (self and others)</summary>
    public uint MoneyFromDiceNumber { get; set; }
    
    
    /// <summary>Sum of the amount of money earned from deals</summary>
    public uint MoneyFromDeals { get; set; }
    
    /// <summary>Sum of the amount of money earned when bankrupting players</summary>
    public uint MoneyFromBankruptPlayers { get; set; }
    
    #endregion
    
    
    //13.4 - Property & Set Economics
    #region 13.4 - Property & Set Economics
    
    /// <summary>
    /// The board index of the property with the highest profitability.
    /// Null if no properties have been sold.
    /// </summary>
    public ushort? MostProfitablePropertyIndex { get; set; }
    
    /// <summary>
    /// The board index of the property with the lowest profitability.
    /// Null if no properties have been sold.
    /// </summary>
    public ushort? LeastProfitablePropertyIndex { get; set; }
    
    /// <summary>
    /// The property set with the highest profitability.
    /// Null if no properties have been sold.
    /// </summary>
    public PropertySet? MostProfitablePropertySet { get; set; }
    
    /// <summary>
    /// The property set with the lowest profitability.
    /// Null if no properties have been sold.
    /// </summary>
    public PropertySet? LeastProfitablePropertySet { get; set; }
    
    
    /// <summary>The maximum number of sets the player had at one time in the game</summary>
    public ushort MaxCompleteSets { get; set; }
    
    /// <summary>The turn number when the player reached the maximum number of sets they had in the game</summary>
    public uint MaxCompleteSetsTurnNumber { get; set; }
    
    
    /// <summary>The total number of properties acquired in the game (buy/auction/reserve/deal - where value &gt; 0)</summary>
    public ushort TotalPropertiesAcquired { get; set; } 
    
    /// <summary>The total number of properties lost in the game (free parking/returned to bank/deal/bankruptcy - where value &lt; 0)</summary>
    public ushort TotalPropertiesLost { get; set; }
    
    
    /// <summary>The total number of properties that were purged in the game</summary>
    public ushort PropertiesPurged { get; set; }
    
    #endregion
    
    
    //13.5 - Dice & Movement

    #region Dice & Movement

    /// <summary>Total number of times the player rolled the dice for their turn</summary>
    public uint TotalTurnRolls { get; set; }
    
    /// <summary>Total number of times the player rolled the dice due to a card action</summary>
    public uint TotalCardRolls { get; set; }
    
    /// <summary>The total number of doubles rolled by the player</summary>
    public uint DoublesRolled { get; set; }
    
    /// <summary>The total number of triples rolled by the player</summary>
    public uint TriplesRolled { get; set; }
    
    
    /// <summary>The total number of times another player rolled this player's dice number</summary>
    public uint TimesSomeoneRolledYourDiceNumber { get; set; }
    
    /// <summary>The total number of times the player rolled their dice number</summary>
    public uint TimesYouRolledYourDiceNumber { get; set; }
    
    
    /// <summary>The total number of times the player changed direction</summary>
    public uint TimesChangedDirection { get; set; }
    
    /// <summary>The sum of board steps the player traversed when moving clockwise</summary>
    public long TotalDistanceTraveledClockwise { get; set; }
    
    /// <summary>The sum of board steps the player traversed when moving counter-clockwise</summary>
    public long TotalDistanceTraveledCounterClockwise { get; set; }
    
    /// <summary>The sum of board steps the player traversed</summary>
    [NotMapped]
    public long TotalDistanceTraveled => TotalDistanceTraveledClockwise + TotalDistanceTraveledCounterClockwise;

    
    /// <summary>The board index of the space the player landed on the most times</summary>
    public ushort MostLandedOnBoardIndex { get; set; }
    
    /// <summary>The total number of times the player landed on GO</summary>
    public uint TimesLandedOnGo { get; set; }
    
    /// <summary>The total number of times the player landed on Free Parking</summary>
    public uint TimesLandedOnFreeParking { get; set; }
    
    /// <summary>The total number of times the player landed on Tax (either tax spaces)</summary>
    public uint TimesLandedOnTax { get; set; }
    
    #endregion
    
    
    //13.6 - Jail
    #region Jail
    
    /// <summary>The total number of times the player was sent to jail</summary>
    public uint TimesSentToJail { get; set; }
    
    /// <summary>The total number of times the player was sent to jail by paying the fine</summary>
    public uint TimesLeftJailByPaying { get; set; }
    
    /// <summary>The total number of times the player was sent to jail by playing a card</summary>
    public uint TimesLeftJailByPlayingCard { get; set; }
    
    /// <summary>The total number of times the player was sent to jail by rolling doubles or triples</summary>
    public uint TimesLeftJailByDice { get; set; }
    
    /// <summary>The total number of times the player left jail</summary>
    [NotMapped]
    public uint TimesLeftJail => TimesLeftJailByPaying + TimesLeftJailByPlayingCard + TimesLeftJailByDice;
    
    /// <summary>The total number of turns the player spent in jail in the game</summary>
    public uint TotalJailTurns { get; set; }
    
    #endregion
    
    
    //13.7 - Free Parking
    #region Free Parking
    
    /// <summary>The total number of properties handed into free parking</summary>
    public uint TotalPropertiesHandedInFP { get; set; }
    
    /// <summary>The total number of properties taken from free parking</summary>
    public uint TotalPropertiesTakenFromFP { get; set; }
    
    /// <summary>
    /// The types of sets handed into free parking in the game.
    /// Null if no properties/set types were handed into free parking.
    /// </summary>
    public string? FPHandedInSetTypesJson { get; set; }
    
    #endregion
    
    
    //13.8 - Loans & Mortgages
    #region Loans & Mortgages
    
    /// <summary>The total number of loans taken by the player in the game</summary>
    public uint TotalLoansTaken { get; set; }
    
    /// <summary>The total amount taken out in loans in the game</summary>
    public uint TotalLoanAmountTaken { get; set; }
    
    /// <summary>The total number of loan repayments repaid by the player in the game</summary>
    public uint TotalLoanRepayments { get; set; }
    
    
    /// <summary>The total number of loans fully repaid off in the game</summary>
    public uint TotalLoansRepaid { get; set; }
    
    /// <summary>The total amount of outstanding debt on loans</summary>
    public uint OutstandingLoanDebt { get; set; }
    
    
    /// <summary>The total number of times the player mortgaged a property</summary>
    public uint TimesMortgaged { get; set; }
    
    /// <summary>The total number of times the player unmortgaged a property</summary> 
    public uint TimesUnmortgaged { get; set; }
    
    /// <summary>The sum of all the mortgage fees paid when passing GO in the game</summary>
    public uint MortgageFeesPaid { get; set; }

    #endregion
    
    
    //TODO: 13.9 - Cards
    
    
    //13.10 - Endgame & Outcome
    #region Endgame & Outcome

    /// <summary>True if the player bankrupted in the game</summary>
    public bool Bankrupted { get; set; }
    
    /// <summary>True if the player voluntarily bankrupted in the game</summary>
    public bool VoluntaryBankruptcy { get; set; }
    
    /// <summary>
    /// The amount that caused the player to declare bankruptcy during a shortfall.
    /// Null if the player did not declare bankruptcy or voluntarily declared bankruptcy.
    /// </summary>
    public uint? BankruptedByAmount { get; set; }
    
    
    /// <summary>The total number of turns survived in the game</summary>
    public int TurnsSurvived { get; set; }
    
    /// <summary>The final balance of the player at the end of the game</summary>
    public uint FinalBalance { get; set; }
    
    /// <summary>The final net worth of the player at the end of the game</summary>
    public long FinalNetWorth { get; set; }
    
    #endregion


    //13.11 State-over-time scalars
    #region State-over-time scalars

    /// <summary>The peak net worth achieved by the player during the game</summary>
    public long PeakNetWorth { get; set; }
    
    /// <summary>The turn number at which the player achieved their peak net worth</summary>
    public int PeakNetWorthTurnNumber { get; set; }
    
    
    /// <summary>The peak balance achieved by the player during the game</summary>
    public uint PeakBalance { get; set; }
    
    /// <summary>The turn number at which the player achieved their peak balance</summary>
    public int PeakBalanceTurnNumber { get; set; }

    #endregion
    
    
    //13.12 - Graph Series JSON
    #region Graph Series JSON

    /// <summary>Series of balance over time (per turn) in json</summary>
    public string BalanceOverTimeJson { get; set; } = string.Empty;
    
    /// <summary>Series of net worth over time (per turn) in json</summary>
    public string NetWorthOverTimeJson { get; set; } = string.Empty;
    
    /// <summary>Series of property count over time (per turn) in json</summary>
    public string PropertyCountOverTimeJson { get; set; } = string.Empty;
    
    /// <summary>Series of wealth rank over time (per turn) in json</summary>
    public string WealthRankOverTimeJson { get; set; } = string.Empty;
    
    #endregion

    public PlayerStatRecord()
    {
    }

    public PlayerStatRecord(List<PlayerStatRecord> records)
    {
        MoneyEarned = (uint)Math.Round(records.Average(x => x.MoneyEarned), MidpointRounding.AwayFromZero);
    }
}