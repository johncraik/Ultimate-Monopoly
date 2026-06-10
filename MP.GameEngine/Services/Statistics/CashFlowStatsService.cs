using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

public class CashFlowStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        uint totalMoneyEarned = 0;
        uint totalMoneySpent = 0;

        uint largestPaymentFinal = 0;
        FinancialReason? largestPaymentReason = null;
        ushort? largestPaymentPropIndex = null;

        uint? largestRentFinal = null;
        ushort? largestRentPropIndex = null;
        
        foreach (var turn in snapshot.Turns)
        {
            var financialEvents = turn.Events
                .OfType<FinancialTransactionReceipt>()
                .Where(fe => fe.PlayerId == player.PlayerId)
                .ToList();

            //Money Earned
            var moneyEarned = financialEvents.Where(fe => fe.Amount > 0)
                .Sum(fe => fe.Amount);
            if (moneyEarned < 0) moneyEarned = 0;   //Defensive
            
            totalMoneyEarned += (uint)moneyEarned;
            
            //Money Spent
            var moneySpentSigned = financialEvents.Where(fe => fe.Amount < 0)
                .Sum(fe => fe.Amount);
            var moneySpent = (uint)Math.Abs(moneySpentSigned);
            
            totalMoneySpent += moneySpent;
            
            //Largest Payment/Rent
            var largestPayment = financialEvents
                .Where(fe => fe.Amount < 0)
                .MinBy(fe => fe.Amount); //Min by since payment is negative
            var largestRent = financialEvents
                .Where(fe => fe is { Amount: < 0, Reason: FinancialReason.Rent })
                .MinBy(fe => fe.Amount);

            if (largestPayment != null)
            {
                //Found a payment
                var lp = (uint)Math.Abs(largestPayment.Amount);
                if(lp > largestPaymentFinal)
                {
                    //Set the largest payment
                    largestPaymentFinal = lp;
                    largestPaymentReason = largestPayment.Reason;
                    
                    //If the largest payment goes to a property, record the index
                    if(largestPayment.Reason is FinancialReason.Purchase 
                       or FinancialReason.Auction
                       or FinancialReason.Build
                       or FinancialReason.MortgageFee
                       or FinancialReason.UnReserve
                       or FinancialReason.Rent)
                        largestPaymentPropIndex = largestPayment.CounterpartyPropertyIndex;
                }
            }

            //Continue if not found largest rent
            if (largestRent == null) continue;

            //Skip only when we already have a larger rent. Note the explicit null check: the
            //accumulator starts null (rent is null until the first payment), and `lr > null`
            //is always false, so a bare `lr > largestRentFinal` would never record anything.
            var lr = (uint)Math.Abs(largestRent.Amount);
            if (largestRentFinal is not null && lr <= largestRentFinal.Value) continue;

            //Set the largest rent (largestRent is always a Rent payment — the where-clause above)
            largestRentFinal = lr;
            largestRentPropIndex = largestRent.CounterpartyPropertyIndex;
        }

        //Set the stats
        record.MoneyEarned = totalMoneyEarned;
        record.MoneySpent = totalMoneySpent;
        
        record.LargestSinglePayment = largestPaymentFinal;
        record.LargestSinglePaymentReason = largestPaymentReason;
        record.LargestSinglePaymentPropertyIndex = largestPaymentPropIndex;
        
        record.LargestRentPayment = largestRentFinal;
        record.LargestRentPaymentPropertyIndex = largestRentPropIndex;
        
        return record;
    }
}