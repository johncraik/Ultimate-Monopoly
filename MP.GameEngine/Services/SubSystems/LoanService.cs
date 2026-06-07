using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

//Manual constructed loan service (NO DI)
//All logic lives inside this class
public class LoanService
{
    private readonly TransactionService _transactionService;

    public LoanService(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }
    
    
    

    public async Task<bool> TakeLoanForShortfall(Framework.GameEngine engine, PlayerModel player, uint shortfallAmount, CancellationToken ct)
    {
        var canTakeLoan = player.CanTakeLoan();
        if (!canTakeLoan)
        {
            engine.CiteRule(RuleCode.Loan_MaxThree);
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Take Loan", 
                $"You already have a maximum of {RuleDictionary.MaxLoans} loans.", ct: ct);
            return false;
        }
        
        //Compute the amount to take out:
        var loanValue = shortfallAmount + RuleDictionary.BalanceToKeep;
        loanValue = MoneyHelper.NormaliseAmountToPositive(loanValue, engine.Cache.RoundingRule, FinancialReason.LoanTake);
        
        var loan = new LoanModel(loanValue);
        player.Loans.Add(loan);
        
        //Cite main loan rules when taking one out:
        engine.CiteRule(RuleCode.Loan_CoversShortfall);
        engine.CiteRule(RuleCode.Loan_KeepUpTo200);
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Loan Taken", 
            $"You have taken out a loan of {RuleDictionary.Currency}{loanValue} for the shortfall of {RuleDictionary.Currency}{shortfallAmount}.", ct: ct);
        
        await _transactionService.TakeLoan(engine, player, loanValue, ct);
        return true;
    }

    public async Task ForcedRepayLoans(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var firstLoan = player.FirstOutstandingLoan();
        if (firstLoan == null)
            return;

        engine.CiteRule(RuleCode.Loan_RepayInstalmentOnGo);
        var amount = player.MinimumLoanRepayment();
        amount = MoneyHelper.NormaliseAmountToPositive(amount, engine.Cache.RoundingRule, FinancialReason.LoanRepay);
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Loan Repayment Made",
            $"You have just paid {RuleDictionary.Currency}{amount} off your oldest loan.", ct: ct);
        
        //Forced payment does not transfer overpay to next loan
        firstLoan.PaidBack += amount;
        engine.CiteRule(RuleCode.Loan_RepaidOldestOverpaymentLost);
        await _transactionService.RepayLoan(engine, player, amount, ct);
    }

    public async Task RepayLoansCustom(Framework.GameEngine engine, uint amount, CancellationToken ct)
    {
        var player = engine.Cache.Game.CurrentPlayer();
        if(player == null)
            return;
        
        var loan = player.FirstOutstandingLoan();
        if (loan == null)
            return;

        //Normalise the amount to pay
        var payAmount = MoneyHelper.NormaliseAmountToPositive(amount, engine.Cache.RoundingRule, FinancialReason.LoanRepay);
        if (player.Money < amount)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Cannot Repay Loan", 
                $"You do not have enough money to repay {RuleDictionary.Currency}{amount}.", ct: ct);
            return;
        }
        
        while (loan != null)
        {
            if(payAmount == 0)
                break;
            
            var outstanding = loan.Amount - loan.PaidBack;

            var loanPayBack = payAmount;
            var oldestPaidOff = false;
            if (payAmount > outstanding)
            {
                //Custom amount will repay all of the oldest loan
                loanPayBack = outstanding;
                payAmount -= outstanding;
                oldestPaidOff = true;
                
                _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Loan Repayment Made",
                    "You have paid off the oldest loan.", ct: ct);
            }
            else
            {
                _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Loan Repayment Made",
                    $"You have just paid {RuleDictionary.Currency}{loanPayBack} off your oldest loan.", ct: ct);
            }
            
            loan.PaidBack += loanPayBack;
            await _transactionService.RepayLoan(engine, player, loanPayBack, ct);
            
            if(!oldestPaidOff)
                //Oldest not paid off yet, so break
                break;
            
            //Get the next loan to carry over payAmount to
            loan = player.FirstOutstandingLoan();
        }
    }
}