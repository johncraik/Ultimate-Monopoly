using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MP.GameEngine.Models.Statistics;

namespace UltimateMonopoly.Models.DataModels.Games;

public class PlayerGameStat : PlayerStatRecord
{
    [MaxLength(38)]
    public string GameId { get; set; }
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; }
    
    [MaxLength(38)]
    public string UserId { get; set; }
    [ForeignKey($"{nameof(GameId)},{nameof(UserId)}")]
    public GamePlayer Player { get; set; }

    public PlayerGameStat()
    {
    }

    public PlayerGameStat(PlayerStatRecord record)
    {
        MoneyEarned = record.MoneyEarned;
        MoneySpent = record.MoneySpent;
        LargestSinglePayment = record.LargestSinglePayment;
        LargestSinglePaymentReason = record.LargestSinglePaymentReason;
        LargestSinglePaymentPropertyIndex = record.LargestSinglePaymentPropertyIndex;
        LargestRentPayment = record.LargestRentPayment;
        LargestRentPaymentPropertyIndex = record.LargestRentPaymentPropertyIndex;
        
        SpentAcquiringProperty = record.SpentAcquiringProperty;
        SpentBuilding = record.SpentBuilding;
        SpentUnmortgaging = record.SpentUnmortgaging;
        SpentOnFines = record.SpentOnFines;
        SpentOnLeavingJail = record.SpentOnLeavingJail;
        SpentOnRepayingLoans = record.SpentOnRepayingLoans;
        RentPaid = record.RentPaid;
        MoneyGivenInDeals = record.MoneyGivenInDeals;
        
        RentEarned = record.RentEarned;
        TimesPassedGo = record.TimesPassedGo;
        MoneyCollectedFromGo = record.MoneyCollectedFromGo;
        MoneyFromSelling = record.MoneyFromSelling;
        MoneyFromCards = record.MoneyFromCards;
        MoneyFromMortgaging = record.MoneyFromMortgaging;
        MoneyFromFreeParking = record.MoneyFromFreeParking;
        MoneyFromTriples = record.MoneyFromTriples;
        MoneyFromSnakeEyes = record.MoneyFromSnakeEyes;
        MoneyFromDiceNumber = record.MoneyFromDiceNumber;
        MoneyFromDeals = record.MoneyFromDeals;
        MoneyFromBankruptPlayers = record.MoneyFromBankruptPlayers;
        
        MostProfitablePropertyIndex = record.MostProfitablePropertyIndex;
        LeastProfitablePropertyIndex = record.LeastProfitablePropertyIndex;
        MostProfitablePropertySet = record.MostProfitablePropertySet;
        LeastProfitablePropertySet = record.LeastProfitablePropertySet;
        MaxCompleteSets = record.MaxCompleteSets;
        MaxCompleteSetsTurnNumber = record.MaxCompleteSetsTurnNumber;
        TotalPropertiesAcquired = record.TotalPropertiesAcquired;
        TotalPropertiesLost = record.TotalPropertiesLost;
        PropertiesPurged = record.PropertiesPurged;
        
        TotalTurnRolls = record.TotalTurnRolls;
        TotalCardRolls = record.TotalCardRolls;
        DoublesRolled = record.DoublesRolled;
        TriplesRolled = record.TriplesRolled;
        TimesSomeoneRolledYourDiceNumber = record.TimesSomeoneRolledYourDiceNumber;
        TimesYouRolledYourDiceNumber = record.TimesYouRolledYourDiceNumber;
        TimesChangedDirection = record.TimesChangedDirection;
        TotalDistanceTraveledClockwise = record.TotalDistanceTraveledClockwise;
        TotalDistanceTraveledCounterClockwise = record.TotalDistanceTraveledCounterClockwise;
        MostLandedOnBoardIndex = record.MostLandedOnBoardIndex;
        TimesLandedOnGo = record.TimesLandedOnGo;
        TimesLandedOnFreeParking = record.TimesLandedOnFreeParking;
        TimesLandedOnTax = record.TimesLandedOnTax;
        
        TimesSentToJail = record.TimesSentToJail;
        TimesLeftJailByPaying = record.TimesLeftJailByPaying;
        TimesLeftJailByPlayingCard = record.TimesLeftJailByPlayingCard;
        TimesLeftJailByDice = record.TimesLeftJailByDice;
        TotalJailTurns = record.TotalJailTurns;
        
        TotalPropertiesHandedInFP = record.TotalPropertiesHandedInFP;
        TotalPropertiesTakenFromFP = record.TotalPropertiesTakenFromFP;
        FPHandedInSetTypesJson = record.FPHandedInSetTypesJson;
        
        TotalLoansTaken = record.TotalLoansTaken;
        TotalLoanAmountTaken = record.TotalLoanAmountTaken;
        TotalLoanRepayments = record.TotalLoanRepayments;
        TotalLoansRepaid = record.TotalLoansRepaid;
        OutstandingLoanDebt = record.OutstandingLoanDebt;
        TimesMortgaged = record.TimesMortgaged;
        TimesUnmortgaged = record.TimesUnmortgaged;
        MortgageFeesPaid = record.MortgageFeesPaid;
        
        Bankrupted = record.Bankrupted;
        VoluntaryBankruptcy = record.VoluntaryBankruptcy;
        BankruptedByAmount = record.BankruptedByAmount;
        TurnsSurvived = record.TurnsSurvived;
        FinalBalance = record.FinalBalance;
        FinalNetWorth = record.FinalNetWorth;
        
        PeakNetWorth = record.PeakNetWorth;
        PeakNetWorthTurnNumber = record.PeakNetWorthTurnNumber;
        PeakBalance = record.PeakBalance;
        PeakBalanceTurnNumber = record.PeakBalanceTurnNumber;
        
        BalanceOverTimeJson = record.BalanceOverTimeJson;
        NetWorthOverTimeJson = record.NetWorthOverTimeJson;
        PropertyCountOverTimeJson = record.PropertyCountOverTimeJson;
        WealthRankOverTimeJson = record.WealthRankOverTimeJson;
    }

    public PlayerGameStat(string gameId, string userId, PlayerStatRecord record)
        : this(record)
    {
        GameId = gameId;
        UserId = userId;
    }
}