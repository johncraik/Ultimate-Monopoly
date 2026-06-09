using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

/// <summary>
/// Loan &amp; mortgage stats (<c>game-stats.md</c> §13.8): loans taken / borrowed / repaid,
/// outstanding debt, and mortgage / unmortgage counts plus GO mortgage fees.
/// </summary>
public class LoanStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        var playerId = player.PlayerId;

        var financial = snapshot.Turns
            .SelectMany(t => t.Events.OfType<FinancialTransactionReceipt>())
            .Where(fe => fe.PlayerId == playerId)
            .ToList();

        uint CountOf(FinancialReason reason) => (uint)financial.Count(fe => fe.Reason == reason);
        uint SumOf(FinancialReason reason) => (uint)financial.Where(fe => fe.Reason == reason).Sum(fe => Math.Abs(fe.Amount));

        // Loans — one LoanTake per loan drawn, one LoanRepay per repayment instalment.
        record.TotalLoansTaken = CountOf(FinancialReason.LoanTake);
        record.TotalLoanAmountTaken = SumOf(FinancialReason.LoanTake);
        record.TotalLoanRepayments = CountOf(FinancialReason.LoanRepay);

        // Mortgages — one Mortgage/Unmortgage per action; MortgageFeesPaid is the summed GO fee.
        record.TimesMortgaged = CountOf(FinancialReason.Mortgage);
        record.TimesUnmortgaged = CountOf(FinancialReason.Unmortgage);
        record.MortgageFeesPaid = SumOf(FinancialReason.MortgageFee);

        // Settlement state from the final snapshot's loan list — loans persist with PaidBack
        // accruing, so a loan is fully repaid when PaidBack >= Amount, and outstanding debt is
        // the remaining (Amount − PaidBack) across loans still owing.
        var finalLoans = snapshot.Turns.LastOrDefault()?.Game.Players
            .FirstOrDefault(p => p.PlayerId == playerId)?.Loans ?? [];

        record.TotalLoansRepaid = (uint)finalLoans.Count(l => !l.IsOutstanding);
        record.OutstandingLoanDebt = (uint)finalLoans.Where(l => l.IsOutstanding).Sum(l => (long)l.Amount - l.PaidBack);

        return record;
    }
}