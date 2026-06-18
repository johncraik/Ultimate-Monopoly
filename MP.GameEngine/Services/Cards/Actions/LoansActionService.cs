using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="LoansAction"/> — wiping (forgiving) or repaying every outstanding loan
/// on the target player(s), plus the compound "wipe-all-and-reward-the-clear" card. The repay leg
/// routes through <see cref="TransactionService"/> (shortfall allowed); the reward leg pays £1000
/// and reuses <see cref="PropertyActionService"/> for the forced property return. See
/// cards-design.md §3 (Loans).
/// </summary>
public class LoansActionService : ICardActionService<LoansAction>
{
    private const uint ClearRewardAmount = 1000;

    private readonly TransactionService _transactionService;
    private readonly ICardActionService<PropertyAction> _propertyActionService;

    /// <summary>Creates the loans-action handler over the transaction seam and the property-action handler its reward leg reuses.</summary>
    public LoansActionService(TransactionService transactionService,
        ICardActionService<PropertyAction> propertyActionService)
    {
        _transactionService = transactionService;
        _propertyActionService = propertyActionService;
    }

    /// <summary>Wipes / repays loans, or runs the wipe-and-reward compound.</summary>
    public async Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, LoansAction action, CancellationToken ct, CardActionContext? context = null)
    {
        if (action.Kind == LoanCardKind.WipeAllAndRewardClear)
        {
            await WipeAllAndRewardClear(engine, ct);
            return true;
        }

        foreach (var target in await CardActionHelper.ResolveTargets(engine, player, action.Target, ct))
        {
            // Snapshot the outstanding loans up front — repaying mutates IsOutstanding as it goes.
            foreach (var loan in target.GetOutstandingLoans())
            {
                var outstanding = loan.Amount - loan.PaidBack;
                if (outstanding == 0)
                    continue;

                // Debit BEFORE marking the loan settled (RepayAll only): a shortfall during repayment must
                // see this loan still outstanding, so it counts toward the max-3 gate and the player can't
                // take a 4th loan to clear it — they pay from genuine funds or go bankrupt. WipeAll just
                // forgives the debt (no money moves).
                if (action.Kind == LoanCardKind.RepayAll)
                    await _transactionService.RepayLoan(engine, target, outstanding, ct);
                loan.PaidBack += outstanding;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Snapshots the players who are already loan-free, wipes everyone's outstanding loans, then
    /// rewards the snapshot-clear players with £1000 each and a forced property return (a player who
    /// owns nothing to give back still keeps the £1000 — the return is a silent no-op).
    /// </summary>
    private async Task WipeAllAndRewardClear(Framework.GameEngine engine, CancellationToken ct)
    {
        // "for all players" — everyone, the holder included.
        var allPlayers = engine.Cache.Game.GetPlayers(excludePovPlayer: false);

        // 1. Snapshot who is currently clear (no outstanding loan) — BEFORE the wipe.
        var clearPlayerIds = allPlayers
            .Where(p => p.GetOutstandingLoans().Count == 0)
            .Select(p => p.PlayerId)
            .ToHashSet();

        // 2. Forgive every player's outstanding loans (no money moves).
        foreach (var p in allPlayers)
            foreach (var loan in p.GetOutstandingLoans())
                loan.PaidBack += loan.Amount - loan.PaidBack;

        // 3. Reward the snapshot-clear players: £1000 from the bank + a forced property return.
        foreach (var p in allPlayers.Where(p => clearPlayerIds.Contains(p.PlayerId)))
        {
            await _transactionService.ReceiveCardPayout(engine, p, ClearRewardAmount, TransactionCounterparty.Bank, null, ct);
            await _propertyActionService.ResolveActionAsync(engine, p,
                new PropertyAction { Kind = PropertyActionKind.ReturnToBank, Target = PlayerTarget.Self, Count = 1 }, ct);
        }
    }
}