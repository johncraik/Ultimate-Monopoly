using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

public class SpendingStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        uint spentAcqProp = 0;
        uint spentBuilding = 0;
        uint spentUnmortgaging = 0;
        uint spentOnFines = 0;
        uint spentLeavingJail = 0;
        uint spentRepayingLoans = 0;
        uint rentPaid = 0;
        uint moneyGivenInDeals = 0;

        foreach (var turn in snapshot.Turns)
        {
            //Filter financial events for this player in this turn
            var financialEvents = turn.Events
                .OfType<FinancialTransactionReceipt>()
                .Where(fe => fe.PlayerId == player.PlayerId && fe.Amount < 0)
                .ToList();

            foreach (var fe in financialEvents)
            {
                //Switch the amount to be positive, and then classify by reason
                var amount = (uint)Math.Abs(fe.Amount);
                switch (fe.Reason)
                {
                    case FinancialReason.Rent:
                        rentPaid += amount;
                        break;
                    case FinancialReason.Purchase:
                    case FinancialReason.Auction:
                    case FinancialReason.UnReserve:
                        spentAcqProp += amount;
                        break;
                    case FinancialReason.Build:
                        spentBuilding += amount;
                        break;
                    case FinancialReason.Unmortgage:
                        spentUnmortgaging += amount;
                        break;
                    case FinancialReason.JailFee:
                        spentLeavingJail += amount;
                        break;
                    case FinancialReason.LoanRepay:
                        spentRepayingLoans += amount;
                        break;
                    case FinancialReason.Deal:
                        moneyGivenInDeals += amount;
                        break;
                    case FinancialReason.Tax:
                    case FinancialReason.FreeParkingPay:
                    case FinancialReason.MortgageFee:
                    case FinancialReason.CardCharge:
                    case FinancialReason.DiceNumBonus:
                        spentOnFines += amount;
                        break;
                }
            }
        }
        
        //Set the stats:
        record.SpentAcquiringProperty = spentAcqProp;
        record.SpentBuilding = spentBuilding;
        record.SpentUnmortgaging = spentUnmortgaging;
        record.SpentOnFines = spentOnFines;
        record.SpentOnLeavingJail = spentLeavingJail;
        record.SpentOnRepayingLoans = spentRepayingLoans;
        record.RentPaid = rentPaid;
        record.MoneyGivenInDeals = moneyGivenInDeals;
        
        return record;
    }
}