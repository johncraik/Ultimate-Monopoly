using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using JC.Core.Models.Auditing;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;

namespace MP.GameEngine.Models.Statistics;

public class PlayerStatRecord : AuditModel
{
    public string PlayerId { get; set; } = string.Empty;
    
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
    
    
    //13.9 - Cards

    #region Cards

    /// <summary>Per-card-type count of cards taken into the hand — serialised <c>Dictionary&lt;CardType,uint&gt;</c>. Per-game only (null in the cross-game aggregate).</summary>
    public string? CardsTakenByTypeJson { get; set; }

    /// <summary>Per-card-type count of cards played — serialised <c>Dictionary&lt;CardType,uint&gt;</c>. Per-game only (null in the cross-game aggregate).</summary>
    public string? CardsPlayedByTypeJson { get; set; }


    /// <summary>Total cards taken into the hand (keep-until-needed) — count of <c>CardTakenReceipt</c>.</summary>
    public uint TotalCardsTaken { get; set; }

    /// <summary>Total cards played/resolved — count of <c>CardPlayedReceipt</c> (instant resolve-on-draw + held plays).</summary>
    public uint TotalCardsPlayed { get; set; }

    /// <summary>Cards taken but never played — taken-receipt occurrences whose CardId never appears in a played receipt.</summary>
    public uint CardsNeverPlayed { get; set; }

    /// <summary>Instant-play cards — played-receipt occurrences whose CardId never appears in a taken receipt (resolve-on-draw).</summary>
    public uint InstantPlayCards { get; set; }

    /// <summary>Number of immunity cards taken into the hand.</summary>
    public uint ImmunityCardsTaken { get; set; }

    /// <summary>Number of immunity cards played.</summary>
    public uint ImmunityCardsPlayed { get; set; }


    /// <summary>The trigger that most often fired a played card — each played receipt's AllTriggers split into individual flags and counted. Null when no triggered card was played.</summary>
    public CardTrigger? MostPlayedTrigger { get; set; }

    /// <summary>The most common engagement of played cards (Forced / Choice / ResolveOnDraw). Null when no card was played.</summary>
    public CardEngagement? MostPlayedEngagement { get; set; }

    #endregion
    
    
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

    public PlayerStatRecord(string playerId)
    {
        PlayerId = playerId;
    }

    public PlayerStatRecord(List<PlayerStatRecord> records, StatisticView statisticView)
    {
        MoneyEarned = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyEarned), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyEarned)?.MoneyEarned ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyEarned)?.MoneyEarned ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyEarned),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneySpent = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneySpent), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneySpent)?.MoneySpent ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneySpent)?.MoneySpent ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneySpent),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };

        var minRecord = records.MinBy(x => x.LargestSinglePayment);
        var maxRecord = records.MaxBy(x => x.LargestSinglePayment);
        LargestSinglePayment = statisticView switch
        {
            //Not total view for single payment
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.LargestSinglePayment), MidpointRounding.AwayFromZero),
            StatisticView.Min => (minRecord?.LargestSinglePayment ?? 0),
            StatisticView.Max => (maxRecord?.LargestSinglePayment ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        LargestSinglePaymentReason = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => MathHelper.Mode(records.Select(x => x.LargestSinglePaymentReason)),
            StatisticView.Min => minRecord?.LargestSinglePaymentReason,
            StatisticView.Max => maxRecord?.LargestSinglePaymentReason,
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        LargestSinglePaymentPropertyIndex = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => MathHelper.Mode(records.Select(x => x.LargestSinglePaymentPropertyIndex)),
            StatisticView.Min => minRecord?.LargestSinglePaymentPropertyIndex,
            StatisticView.Max => maxRecord?.LargestSinglePaymentPropertyIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };

        minRecord = records.MinBy(x => x.LargestRentPayment);
        maxRecord = records.MaxBy(x => x.LargestRentPayment);
        LargestRentPayment = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.LargestRentPayment) ?? 0, MidpointRounding.AwayFromZero),
            StatisticView.Min => (minRecord?.LargestRentPayment ?? 0),
            StatisticView.Max => (maxRecord?.LargestRentPayment ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        LargestRentPaymentPropertyIndex = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => MathHelper.Mode(records.Select(x => x.LargestRentPaymentPropertyIndex)),
            StatisticView.Min => minRecord?.LargestRentPaymentPropertyIndex,
            StatisticView.Max => maxRecord?.LargestRentPaymentPropertyIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        
        
        SpentAcquiringProperty = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.SpentAcquiringProperty), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.SpentAcquiringProperty)?.SpentAcquiringProperty ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.SpentAcquiringProperty)?.SpentAcquiringProperty ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.SpentAcquiringProperty),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        SpentBuilding = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.SpentBuilding), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.SpentBuilding)?.SpentBuilding ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.SpentBuilding)?.SpentBuilding ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.SpentBuilding),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        SpentUnmortgaging = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.SpentUnmortgaging), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.SpentUnmortgaging)?.SpentUnmortgaging ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.SpentUnmortgaging)?.SpentUnmortgaging ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.SpentUnmortgaging),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        SpentOnFines = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.SpentOnFines), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.SpentOnFines)?.SpentOnFines ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.SpentOnFines)?.SpentOnFines ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.SpentOnFines),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        SpentOnLeavingJail = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.SpentOnLeavingJail), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.SpentOnLeavingJail)?.SpentOnLeavingJail ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.SpentOnLeavingJail)?.SpentOnLeavingJail ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.SpentOnLeavingJail),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        SpentOnRepayingLoans = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.SpentOnRepayingLoans), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.SpentOnRepayingLoans)?.SpentOnRepayingLoans ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.SpentOnRepayingLoans)?.SpentOnRepayingLoans ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.SpentOnRepayingLoans),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        RentPaid = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.RentPaid), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.RentPaid)?.RentPaid ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.RentPaid)?.RentPaid ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.RentPaid),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyGivenInDeals = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyGivenInDeals), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyGivenInDeals)?.MoneyGivenInDeals ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyGivenInDeals)?.MoneyGivenInDeals ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyGivenInDeals),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };


        //13.3 - Income Breakdown (money — Total is meaningful)
        RentEarned = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.RentEarned), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.RentEarned)?.RentEarned ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.RentEarned)?.RentEarned ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.RentEarned),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesPassedGo = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.TimesPassedGo), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesPassedGo)?.TimesPassedGo ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesPassedGo)?.TimesPassedGo ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.TimesPassedGo),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyCollectedFromGo = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyCollectedFromGo), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyCollectedFromGo)?.MoneyCollectedFromGo ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyCollectedFromGo)?.MoneyCollectedFromGo ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyCollectedFromGo),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyFromSelling = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromSelling), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromSelling)?.MoneyFromSelling ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromSelling)?.MoneyFromSelling ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromSelling),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyFromCards = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromCards), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromCards)?.MoneyFromCards ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromCards)?.MoneyFromCards ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromCards),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };

        // 13.9 - Cards. Scalar counts aggregate like the other totals; the categoricals use Mode (avg/max/total)
        // / LeastCommon (min) like the property categoricals; the per-type JSON dicts stay per-game (left null).
        TotalCardsTaken = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.TotalCardsTaken), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalCardsTaken)?.TotalCardsTaken ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalCardsTaken)?.TotalCardsTaken ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.TotalCardsTaken),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalCardsPlayed = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.TotalCardsPlayed), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalCardsPlayed)?.TotalCardsPlayed ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalCardsPlayed)?.TotalCardsPlayed ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.TotalCardsPlayed),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        CardsNeverPlayed = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.CardsNeverPlayed), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.CardsNeverPlayed)?.CardsNeverPlayed ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.CardsNeverPlayed)?.CardsNeverPlayed ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.CardsNeverPlayed),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        InstantPlayCards = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.InstantPlayCards), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.InstantPlayCards)?.InstantPlayCards ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.InstantPlayCards)?.InstantPlayCards ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.InstantPlayCards),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        ImmunityCardsTaken = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.ImmunityCardsTaken), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.ImmunityCardsTaken)?.ImmunityCardsTaken ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.ImmunityCardsTaken)?.ImmunityCardsTaken ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.ImmunityCardsTaken),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        ImmunityCardsPlayed = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.ImmunityCardsPlayed), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.ImmunityCardsPlayed)?.ImmunityCardsPlayed ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.ImmunityCardsPlayed)?.ImmunityCardsPlayed ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.ImmunityCardsPlayed),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MostPlayedTrigger = statisticView switch
        {
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.MostPlayedTrigger)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.MostPlayedTrigger)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MostPlayedEngagement = statisticView switch
        {
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.MostPlayedEngagement)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.MostPlayedEngagement)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };

        MoneyFromMortgaging = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromMortgaging), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromMortgaging)?.MoneyFromMortgaging ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromMortgaging)?.MoneyFromMortgaging ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromMortgaging),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyFromFreeParking = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromFreeParking), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromFreeParking)?.MoneyFromFreeParking ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromFreeParking)?.MoneyFromFreeParking ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromFreeParking),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyFromTriples = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromTriples), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromTriples)?.MoneyFromTriples ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromTriples)?.MoneyFromTriples ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromTriples),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyFromSnakeEyes = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromSnakeEyes), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromSnakeEyes)?.MoneyFromSnakeEyes ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromSnakeEyes)?.MoneyFromSnakeEyes ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromSnakeEyes),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyFromDiceNumber = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromDiceNumber), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromDiceNumber)?.MoneyFromDiceNumber ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromDiceNumber)?.MoneyFromDiceNumber ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromDiceNumber),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyFromDeals = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromDeals), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromDeals)?.MoneyFromDeals ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromDeals)?.MoneyFromDeals ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromDeals),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MoneyFromBankruptPlayers = statisticView switch
        {
            StatisticView.Average => (uint)Math.Round(records.Average(x => x.MoneyFromBankruptPlayers), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MoneyFromBankruptPlayers)?.MoneyFromBankruptPlayers ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MoneyFromBankruptPlayers)?.MoneyFromBankruptPlayers ?? 0),
            StatisticView.Total => (uint)records.Sum(x => x.MoneyFromBankruptPlayers),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };


        //13.4 - Property & Set Economics (Total drops outside the money sections)
        //Unpaired "most/least" categoricals — most common for Average/Max/Total, least common for Min.
        MostProfitablePropertyIndex = statisticView switch
        {
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.MostProfitablePropertyIndex)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.MostProfitablePropertyIndex)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        LeastProfitablePropertyIndex = statisticView switch
        {
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.LeastProfitablePropertyIndex)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.LeastProfitablePropertyIndex)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MostProfitablePropertySet = statisticView switch
        {
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.MostProfitablePropertySet)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.MostProfitablePropertySet)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        LeastProfitablePropertySet = statisticView switch
        {
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.LeastProfitablePropertySet)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.LeastProfitablePropertySet)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };

        minRecord = records.MinBy(x => x.MaxCompleteSets);
        maxRecord = records.MaxBy(x => x.MaxCompleteSets);
        MaxCompleteSets = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (ushort)Math.Round(records.Average(x => x.MaxCompleteSets), MidpointRounding.AwayFromZero),
            StatisticView.Min => (minRecord?.MaxCompleteSets ?? 0),
            StatisticView.Max => (maxRecord?.MaxCompleteSets ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MaxCompleteSetsTurnNumber = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.MaxCompleteSetsTurnNumber), MidpointRounding.AwayFromZero),
            StatisticView.Min => (minRecord?.MaxCompleteSetsTurnNumber ?? 0),
            StatisticView.Max => (maxRecord?.MaxCompleteSetsTurnNumber ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };

        TotalPropertiesAcquired = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (ushort)Math.Round(records.Average(x => x.TotalPropertiesAcquired), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalPropertiesAcquired)?.TotalPropertiesAcquired ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalPropertiesAcquired)?.TotalPropertiesAcquired ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalPropertiesLost = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (ushort)Math.Round(records.Average(x => x.TotalPropertiesLost), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalPropertiesLost)?.TotalPropertiesLost ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalPropertiesLost)?.TotalPropertiesLost ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        PropertiesPurged = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (ushort)Math.Round(records.Average(x => x.PropertiesPurged), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.PropertiesPurged)?.PropertiesPurged ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.PropertiesPurged)?.PropertiesPurged ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };


        //13.5 - Dice & Movement
        TotalTurnRolls = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalTurnRolls), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalTurnRolls)?.TotalTurnRolls ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalTurnRolls)?.TotalTurnRolls ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalCardRolls = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalCardRolls), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalCardRolls)?.TotalCardRolls ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalCardRolls)?.TotalCardRolls ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        DoublesRolled = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.DoublesRolled), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.DoublesRolled)?.DoublesRolled ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.DoublesRolled)?.DoublesRolled ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TriplesRolled = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TriplesRolled), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TriplesRolled)?.TriplesRolled ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TriplesRolled)?.TriplesRolled ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesSomeoneRolledYourDiceNumber = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesSomeoneRolledYourDiceNumber), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesSomeoneRolledYourDiceNumber)?.TimesSomeoneRolledYourDiceNumber ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesSomeoneRolledYourDiceNumber)?.TimesSomeoneRolledYourDiceNumber ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesYouRolledYourDiceNumber = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesYouRolledYourDiceNumber), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesYouRolledYourDiceNumber)?.TimesYouRolledYourDiceNumber ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesYouRolledYourDiceNumber)?.TimesYouRolledYourDiceNumber ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesChangedDirection = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesChangedDirection), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesChangedDirection)?.TimesChangedDirection ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesChangedDirection)?.TimesChangedDirection ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalDistanceTraveledClockwise = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (long)Math.Round(records.Average(x => x.TotalDistanceTraveledClockwise), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalDistanceTraveledClockwise)?.TotalDistanceTraveledClockwise ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalDistanceTraveledClockwise)?.TotalDistanceTraveledClockwise ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalDistanceTraveledCounterClockwise = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (long)Math.Round(records.Average(x => x.TotalDistanceTraveledCounterClockwise), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalDistanceTraveledCounterClockwise)?.TotalDistanceTraveledCounterClockwise ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalDistanceTraveledCounterClockwise)?.TotalDistanceTraveledCounterClockwise ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MostLandedOnBoardIndex = statisticView switch
        {
            //Board index — a "most landed on" categorical: most common space across games (least common for Min).
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.MostLandedOnBoardIndex)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.MostLandedOnBoardIndex)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesLandedOnGo = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesLandedOnGo), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesLandedOnGo)?.TimesLandedOnGo ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesLandedOnGo)?.TimesLandedOnGo ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesLandedOnFreeParking = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesLandedOnFreeParking), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesLandedOnFreeParking)?.TimesLandedOnFreeParking ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesLandedOnFreeParking)?.TimesLandedOnFreeParking ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesLandedOnTax = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesLandedOnTax), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesLandedOnTax)?.TimesLandedOnTax ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesLandedOnTax)?.TimesLandedOnTax ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };


        //13.6 - Jail
        TimesSentToJail = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesSentToJail), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesSentToJail)?.TimesSentToJail ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesSentToJail)?.TimesSentToJail ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesLeftJailByPaying = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesLeftJailByPaying), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesLeftJailByPaying)?.TimesLeftJailByPaying ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesLeftJailByPaying)?.TimesLeftJailByPaying ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesLeftJailByPlayingCard = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesLeftJailByPlayingCard), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesLeftJailByPlayingCard)?.TimesLeftJailByPlayingCard ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesLeftJailByPlayingCard)?.TimesLeftJailByPlayingCard ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesLeftJailByDice = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesLeftJailByDice), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesLeftJailByDice)?.TimesLeftJailByDice ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesLeftJailByDice)?.TimesLeftJailByDice ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalJailTurns = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalJailTurns), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalJailTurns)?.TotalJailTurns ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalJailTurns)?.TotalJailTurns ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };


        //13.7 - Free Parking
        TotalPropertiesHandedInFP = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalPropertiesHandedInFP), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalPropertiesHandedInFP)?.TotalPropertiesHandedInFP ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalPropertiesHandedInFP)?.TotalPropertiesHandedInFP ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalPropertiesTakenFromFP = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalPropertiesTakenFromFP), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalPropertiesTakenFromFP)?.TotalPropertiesTakenFromFP ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalPropertiesTakenFromFP)?.TotalPropertiesTakenFromFP ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        //FPHandedInSetTypesJson is a per-game list of set types — no mean/min/max/mode across games, so left null.


        //13.8 - Loans & Mortgages (Total drops outside the money sections)
        TotalLoansTaken = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalLoansTaken), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalLoansTaken)?.TotalLoansTaken ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalLoansTaken)?.TotalLoansTaken ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalLoanAmountTaken = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalLoanAmountTaken), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalLoanAmountTaken)?.TotalLoanAmountTaken ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalLoanAmountTaken)?.TotalLoanAmountTaken ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalLoanRepayments = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalLoanRepayments), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalLoanRepayments)?.TotalLoanRepayments ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalLoanRepayments)?.TotalLoanRepayments ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TotalLoansRepaid = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TotalLoansRepaid), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TotalLoansRepaid)?.TotalLoansRepaid ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TotalLoansRepaid)?.TotalLoansRepaid ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        OutstandingLoanDebt = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.OutstandingLoanDebt), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.OutstandingLoanDebt)?.OutstandingLoanDebt ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.OutstandingLoanDebt)?.OutstandingLoanDebt ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesMortgaged = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesMortgaged), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesMortgaged)?.TimesMortgaged ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesMortgaged)?.TimesMortgaged ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TimesUnmortgaged = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.TimesUnmortgaged), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TimesUnmortgaged)?.TimesUnmortgaged ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TimesUnmortgaged)?.TimesUnmortgaged ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        MortgageFeesPaid = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.MortgageFeesPaid), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.MortgageFeesPaid)?.MortgageFeesPaid ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.MortgageFeesPaid)?.MortgageFeesPaid ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };


        //13.10 - Endgame & Outcome
        //Bools — most common for Average/Max/Total, least common for Min.
        Bankrupted = statisticView switch
        {
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.Bankrupted)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.Bankrupted)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        VoluntaryBankruptcy = statisticView switch
        {
            StatisticView.Average or StatisticView.Max or StatisticView.Total => MathHelper.Mode(records.Select(x => x.VoluntaryBankruptcy)),
            StatisticView.Min => MathHelper.LeastCommon(records.Select(x => x.VoluntaryBankruptcy)),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        //Null (not 0) when no game ended in bankruptcy — a £0 shortfall isn't a real bankruptcy.
        BankruptedByAmount = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => records.Average(x => x.BankruptedByAmount) is { } avg
                ? (uint)Math.Round(avg, MidpointRounding.AwayFromZero)
                : null,
            StatisticView.Min => records.MinBy(x => x.BankruptedByAmount)?.BankruptedByAmount,
            StatisticView.Max => records.MaxBy(x => x.BankruptedByAmount)?.BankruptedByAmount,
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        TurnsSurvived = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (int)Math.Round(records.Average(x => x.TurnsSurvived), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.TurnsSurvived)?.TurnsSurvived ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.TurnsSurvived)?.TurnsSurvived ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        FinalBalance = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.FinalBalance), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.FinalBalance)?.FinalBalance ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.FinalBalance)?.FinalBalance ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        FinalNetWorth = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (long)Math.Round(records.Average(x => x.FinalNetWorth), MidpointRounding.AwayFromZero),
            StatisticView.Min => (records.MinBy(x => x.FinalNetWorth)?.FinalNetWorth ?? 0),
            StatisticView.Max => (records.MaxBy(x => x.FinalNetWorth)?.FinalNetWorth ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };


        //13.11 - State-over-time scalars
        minRecord = records.MinBy(x => x.PeakNetWorth);
        maxRecord = records.MaxBy(x => x.PeakNetWorth);
        PeakNetWorth = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (long)Math.Round(records.Average(x => x.PeakNetWorth), MidpointRounding.AwayFromZero),
            StatisticView.Min => (minRecord?.PeakNetWorth ?? 0),
            StatisticView.Max => (maxRecord?.PeakNetWorth ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        PeakNetWorthTurnNumber = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (int)Math.Round(records.Average(x => x.PeakNetWorthTurnNumber), MidpointRounding.AwayFromZero),
            StatisticView.Min => (minRecord?.PeakNetWorthTurnNumber ?? 0),
            StatisticView.Max => (maxRecord?.PeakNetWorthTurnNumber ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };

        minRecord = records.MinBy(x => x.PeakBalance);
        maxRecord = records.MaxBy(x => x.PeakBalance);
        PeakBalance = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (uint)Math.Round(records.Average(x => x.PeakBalance), MidpointRounding.AwayFromZero),
            StatisticView.Min => (minRecord?.PeakBalance ?? 0),
            StatisticView.Max => (maxRecord?.PeakBalance ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        PeakBalanceTurnNumber = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => (int)Math.Round(records.Average(x => x.PeakBalanceTurnNumber), MidpointRounding.AwayFromZero),
            StatisticView.Min => (minRecord?.PeakBalanceTurnNumber ?? 0),
            StatisticView.Max => (maxRecord?.PeakBalanceTurnNumber ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };


        //13.12 - Graph Series JSON (element-wise across games; no Total — folds into the average)
        Func<IReadOnlyList<double>, double> seriesAggregator = statisticView switch
        {
            StatisticView.Average or StatisticView.Total => xs => xs.Average(),
            StatisticView.Min => xs => xs.Min(),
            StatisticView.Max => xs => xs.Max(),
            _ => throw new ArgumentOutOfRangeException(nameof(statisticView), statisticView, null)
        };
        BalanceOverTimeJson = JsonSerializer.Serialize(
            MathHelper.AggregateSeries(records.Select(x => x.BalanceOverTimeJson), seriesAggregator)
                .Select(v => (uint)Math.Round(v, MidpointRounding.AwayFromZero)));
        NetWorthOverTimeJson = JsonSerializer.Serialize(
            MathHelper.AggregateSeries(records.Select(x => x.NetWorthOverTimeJson), seriesAggregator)
                .Select(v => (long)Math.Round(v, MidpointRounding.AwayFromZero)));
        PropertyCountOverTimeJson = JsonSerializer.Serialize(
            MathHelper.AggregateSeries(records.Select(x => x.PropertyCountOverTimeJson), seriesAggregator)
                .Select(v => (int)Math.Round(v, MidpointRounding.AwayFromZero)));
        WealthRankOverTimeJson = JsonSerializer.Serialize(
            MathHelper.AggregateSeries(records.Select(x => x.WealthRankOverTimeJson), seriesAggregator)
                .Select(v => (int)Math.Round(v, MidpointRounding.AwayFromZero)));
    }
}