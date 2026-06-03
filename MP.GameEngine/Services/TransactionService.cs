using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services;

/// <summary>
/// Owns every money movement in the engine. Each public method maps to a single
/// <see cref="FinancialReason"/> for readable call sites and a consistent
/// receipt-emission surface.
///
/// The service does <i>only</i> money: it does not change property ownership,
/// mark properties mortgaged/reserved, or trigger downstream rule effects.
/// Those mutations are the caller's job (typically PropertyService or an
/// orchestrator) and happen <i>after</i> the relevant transaction method
/// returns successfully.
///
/// Sign convention everywhere here matches <c>FinancialTransactionReceipt.Amount</c>:
/// positive = the subject player received, negative = paid. Each public method
/// names the direction so call sites never have to think about signs.
/// </summary>
public class TransactionService
{
    // ───────────────────── Reasons that DEBIT the player ─────────────────────

    /// <summary>
    /// Rent paid by <paramref name="player"/> to the property's owner. Silently
    /// no-ops when rent is not collectible (no owner, mortgaged, reserved,
    /// owner in jail per <c>game-rules.md</c> Default rule 2). Triggers a
    /// shortfall prompt if the player can't afford the rent.
    /// </summary>
    public async Task PayRent(Framework.GameEngine engine, PlayerModel player, uint rent, ushort propertyIndex, CancellationToken ct)
    {
        //Safeguard checks repeated here as well as call sight:
        var space = engine.Cache.Board.GetBoardSpace(propertyIndex);
        if (!space.IsRentable) return;

        var property = engine.Cache.Game.GetPropertySpace(propertyIndex);
        if (property?.OwnerPlayerId is null) return;
        if (property.State is not PropertyState.Owned) return;          // mortgaged/reserved → no rent

        var owner = engine.Cache.Game.GetPlayer(property.OwnerPlayerId);
        if (owner is null) return;                                       // bankrupt owner; shouldn't own anything but defensive
        if (owner.IsInJail) return;                                      // game-rules Default rule 2

        //Amount is passed in, computed/retrieved from call sight and not checked
        await Move(engine, player, -rent, FinancialReason.Rent,
            counterparty: TransactionCounterparty.Player,
            counterpartyPlayer: owner,
            counterpartyPropertyIndex: propertyIndex,
            allowShortfall: true,
            ct: ct);
    }

    /// <summary>Tax-space payment — flows from <paramref name="player"/> into the Free Parking pot. Shortfall allowed.</summary>
    public Task PayTax(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.Tax,
            counterparty: TransactionCounterparty.FreeParking,
            allowShortfall: true,
            ct: ct);

    /// <summary>Pays the player's current jail fee into Free Parking. Shortfall allowed.</summary>
    public Task PayJailFee(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
        => Move(engine, player, -player.JailCost, FinancialReason.JailFee,
            counterparty: TransactionCounterparty.FreeParking,
            allowShortfall: true,
            ct: ct);

    /// <summary>
    /// Generic payment into Free Parking (e.g. the no-properties FP-landing payment
    /// per game-rules.md Free Parking rule 2). Shortfall allowed.
    /// </summary>
    public Task PayIntoFreeParking(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.FreeParkingPay,
            counterparty: TransactionCounterparty.FreeParking,
            allowShortfall: true,
            ct: ct);

    /// <summary>
    /// Loan repayment — flows from <paramref name="player"/> to the bank. Shortfall allowed
    /// (a player may take a new loan to cover an instalment per game-rules.md Loans rule 5).
    /// </summary>
    public Task RepayLoan(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.LoanRepay,
            counterparty: TransactionCounterparty.Bank,
            allowShortfall: true,
            ct: ct);

    /// <summary>
    /// The 10% mortgage fee paid each time a player passes GO with mortgaged
    /// properties (game-rules.md Mortgaging rule 1). Per-property. Shortfall allowed.
    /// </summary>
    public Task PayMortgageFee(Framework.GameEngine engine, PlayerModel player, uint amount, ushort counterpartyPropertyIndex, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.MortgageFee,
            counterparty: TransactionCounterparty.Bank,
            counterpartyPropertyIndex: counterpartyPropertyIndex,
            allowShortfall: true,
            ct: ct);

    /// <summary>
    /// Property purchase. The caller is responsible for the affordability pre-gate
    /// (Default rule 7 forbids raising funds to buy) and for mutating ownership via
    /// <c>PropertyModel.OwnProperty(...)</c> after a successful call.
    /// </summary>
    public Task PurchaseProperty(Framework.GameEngine engine, PlayerModel player, uint amount, ushort counterpartyPropertyIndex, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.Purchase,
            counterparty: TransactionCounterparty.Bank,
            counterpartyPropertyIndex: counterpartyPropertyIndex,
            allowShortfall: false,
            ct: ct);

    /// <summary>
    /// Removes the reserve status from a property for the specified <paramref name="player"/>.
    /// Updates the financial state by deducting the specified <paramref name="amount"/>
    /// and assigns the transaction reason as "UnReserve". The transaction is conducted with the bank,
    /// targeting the property identified by <paramref name="counterpartyPropertyIndex"/>.
    /// The operation does not permit shortfalls.
    /// </summary>
    public Task UnReserveProperty(Framework.GameEngine engine, PlayerModel player, uint amount, ushort counterpartyPropertyIndex, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.UnReserve,
            counterparty: TransactionCounterparty.Bank,
            counterpartyPropertyIndex: counterpartyPropertyIndex,
            allowShortfall: false,
            ct: ct);

    /// <summary>
    /// Winning bid payment for an auctioned property. Caller pre-gated the bid;
    /// raising funds during an auction is not allowed (Default rule 7).
    /// </summary>
    public Task WinAuction(Framework.GameEngine engine, PlayerModel player, uint amount, ushort counterpartyPropertyIndex, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.Auction,
            counterparty: TransactionCounterparty.Bank,
            counterpartyPropertyIndex: counterpartyPropertyIndex,
            allowShortfall: false,
            ct: ct);

    /// <summary>
    /// Build cost paid to the bank. Caller pre-gates affordability and applies the
    /// building level change on the property after a successful call.
    /// </summary>
    public Task PayForBuild(Framework.GameEngine engine, PlayerModel player, uint amount, ushort counterpartyPropertyIndex, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.Build,
            counterparty: TransactionCounterparty.Bank,
            counterpartyPropertyIndex: counterpartyPropertyIndex,
            allowShortfall: false,
            ct: ct);

    /// <summary>
    /// Cost paid to lift a mortgage. Caller pre-gates affordability and flips the
    /// property's <c>State</c> back to <see cref="PropertyState.Owned"/> on success.
    /// </summary>
    public Task PayToUnmortgage(Framework.GameEngine engine, PlayerModel player, uint amount, ushort counterpartyPropertyIndex, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.Unmortgage,
            counterparty: TransactionCounterparty.Bank,
            counterpartyPropertyIndex: counterpartyPropertyIndex,
            allowShortfall: false,
            ct: ct);

    /// <summary>
    /// Card-driven debit. <paramref name="counterparty"/> + optional
    /// <paramref name="counterpartyPlayer"/> describe where the money goes
    /// (Bank, Free Parking, or a named player). Shortfall allowed by default —
    /// the card may pre-gate where required.
    /// </summary>
    public Task PayCardCharge(Framework.GameEngine engine, PlayerModel player, uint amount,
        TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.CardCharge,
            counterparty: counterparty,
            counterpartyPlayer: counterpartyPlayer,
            allowShortfall: true,
            ct: ct);

    public Task PayDiceBonus(Framework.GameEngine engine, PlayerModel player, PlayerModel counterpartyPlayer, CancellationToken ct)
        => Move(engine, player, -RuleDictionary.DiceNumRolledBonus, FinancialReason.DiceNumBonus,
            counterparty: TransactionCounterparty.Player,
            counterpartyPlayer: counterpartyPlayer,
            allowShortfall: true,
            ct: ct);


    // ───────────────────── Reasons that CREDIT the player ─────────────────────

    /// <summary>GO bonus (or partial bonus for anti-clockwise crossing) — Bank → player.</summary>
    public Task ReceiveGoBonus(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.GoBonus,
            counterparty: TransactionCounterparty.Bank,
            ct: ct);
    
    public Task ReceiveDiceBonus(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
        => Move(engine, player, RuleDictionary.DiceNumRolledBonus, FinancialReason.DiceNumBonus,
            counterparty: TransactionCounterparty.Bank,
            ct: ct);

    /// <summary>Free Parking pot → player (FP-landing payout, etc.).</summary>
    public Task TakeFromFreeParking(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.FreeParkingTake,
            counterparty: TransactionCounterparty.FreeParking,
            ct: ct);

    /// <summary>
    /// Loan disbursement — Bank → player. Caller is responsible for adding the
    /// <c>LoanModel</c> to <c>player.Loans</c> in the same logical operation.
    /// </summary>
    public Task TakeLoan(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.LoanTake,
            counterparty: TransactionCounterparty.Bank,
            ct: ct);

    /// <summary>Building sell-back — Bank → player. Caller drops the building level on the property.</summary>
    public Task ReceiveForSell(Framework.GameEngine engine, PlayerModel player, uint amount, ushort counterpartyPropertyIndex, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.Sell,
            counterparty: TransactionCounterparty.Bank,
            counterpartyPropertyIndex: counterpartyPropertyIndex,
            ct: ct);

    /// <summary>Mortgage payout — Bank → player. Caller flips the property's State to Mortgaged.</summary>
    public Task ReceiveForMortgage(Framework.GameEngine engine, PlayerModel player, uint amount, ushort counterpartyPropertyIndex, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.Mortgage,
            counterparty: TransactionCounterparty.Bank,
            counterpartyPropertyIndex: counterpartyPropertyIndex,
            ct: ct);

    /// <summary>
    /// Card-driven credit. <paramref name="counterparty"/> + optional
    /// <paramref name="counterpartyPlayer"/> describe where the money comes from.
    /// </summary>
    public Task ReceiveCardPayout(Framework.GameEngine engine, PlayerModel player, uint amount,
        TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.CardPayout,
            counterparty: counterparty,
            counterpartyPlayer: counterpartyPlayer,
            ct: ct);

    public Task ReceiveSnakeEyes(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
        => Move(engine, player, RuleDictionary.SnakeEyesBonus, FinancialReason.SneakEyes, 
            counterparty: TransactionCounterparty.Bank,
            ct: ct);

    // ───────────────────── Player-to-player ─────────────────────

    /// <summary>
    /// Money component of a player-to-player deal. <paramref name="amount"/> is signed
    /// from <paramref name="player"/>'s perspective — positive = receive from
    /// <paramref name="otherPlayer"/>, negative = pay <paramref name="otherPlayer"/>.
    /// Shortfall allowed when paying.
    /// </summary>
    public Task ProcessDealPayment(Framework.GameEngine engine, PlayerModel player, PlayerModel otherPlayer, long amount, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.Deal,
            counterparty: TransactionCounterparty.Player,
            counterpartyPlayer: otherPlayer,
            allowShortfall: amount < 0,
            ct: ct);


    // ───────────────────── Core helper ─────────────────────

    /// <summary>
    /// Applies a signed money movement to <paramref name="player"/> and, where one
    /// exists, the counterparty (another player or the Free Parking pot — the Bank
    /// is untracked). On a debit-shortfall, opens a <see cref="ShortfallPrompt"/>
    /// if <paramref name="allowShortfall"/> is true; otherwise silently no-ops
    /// (per the engine policy — mutations against the rules don't throw).
    ///
    /// Emits one <see cref="FinancialTransactionReceipt"/> from each affected
    /// player's perspective. Mutations land on the cache working copy and are
    /// <b>not</b> committed here — promotion to committed state happens only at
    /// the turn-state boundary (<see cref="GameCacheModel.SetTurnState"/>).
    /// Committing mid-move would promote <c>_working</c> to <c>_game</c> and
    /// detach the player references this (and any later) move still holds. See
    /// <c>design-docs/transactions.md</c> §7.
    /// </summary>
    private async Task Move(
        Framework.GameEngine engine,
        PlayerModel player,
        long amount,
        FinancialReason reason,
        TransactionCounterparty counterparty = TransactionCounterparty.Bank,
        PlayerModel? counterpartyPlayer = null,
        ushort? counterpartyPropertyIndex = null,
        bool allowShortfall = false,
        CancellationToken ct = default)
    {
        if(amount == 0) return;
        
        amount = MoneyHelper.NormaliseAmount(amount, engine.Cache.RoundingRule, reason);
        switch (amount)
        {
            case 0:
                return;
            case < 0:
            {
                var debit = (uint)(-amount);
                if (player.Money < debit)
                {
                    if (!allowShortfall) return;        // caller pre-gated and got it wrong → silent no-op

                    var outcome = await ResolveShortfall(engine, player, debit,
                        counterpartyPlayer?.PlayerId, counterpartyPropertyIndex, ct);

                    switch (outcome)
                    {
                        case ShortfallOutcome.DebtSettled:
                        case ShortfallOutcome.Bankrupted:
                            // The debt is no longer owed — via creditor-deal or bankruptcy.
                            // The original transaction must not apply; receipts for the
                            // settling path are emitted by the relevant sub-service.
                            return;

                        case ShortfallOutcome.FundsRaised:
                            // The sub-service raised cash (loan / mortgage / sell-building).
                            // The original transaction continues below.
                            if (player.Money < debit) return;   // shouldn't happen, but defensive
                            break;
                    }
                }

                break;
            }
        }

        ApplyBalances(engine, player, amount, counterparty, counterpartyPlayer);
        EmitReceipts(engine, player, amount, reason, counterparty, counterpartyPlayer, counterpartyPropertyIndex);
        
        //This has been kept for reference that save changes should NEVER be called inside services
        //Save changes will detatch objects (like players) from the GameModel (since _working is promoted to _game)
        //Save changes should only EVERY be called at turn state boundaries (turn state provider), where we have no detatched objects
        //engine.Cache.SaveChanges();
    }

    private void ApplyBalances(
        Framework.GameEngine engine, PlayerModel player, long amount,
        TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer)
    {
        // Player update — signed: amount > 0 credits, amount < 0 debits.
        player.Money = (uint)(player.Money + amount);

        if (counterpartyPlayer is not null)
        {
            // Counterparty player gets the opposite sign — they paid what player received and vice versa.
            counterpartyPlayer.Money = (uint)(counterpartyPlayer.Money - amount);
            return;
        }

        if (counterparty == TransactionCounterparty.FreeParking)
        {
            // amount > 0: player took from FP → pot decreases.
            // amount < 0: player paid into FP → pot increases.
            engine.Cache.Game.FreeParkingAmount =
                (uint)(engine.Cache.Game.FreeParkingAmount - amount);
        }
        // Bank is untracked — no balance to mutate.
    }

    private void EmitReceipts(
        Framework.GameEngine engine, PlayerModel player, long amount, FinancialReason reason,
        TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer,
        ushort? counterpartyPropertyIndex)
    {
        // Subject player's perspective.
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = amount,
            Reason = reason,
            Counterparty = counterparty,
            CounterpartyPlayerId = counterpartyPlayer?.PlayerId,
            CounterpartyPropertyIndex = counterpartyPropertyIndex
        });

        if (counterpartyPlayer is null) return;

        // Counterparty player's perspective — opposite sign, mirrored counterparty.
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = counterpartyPlayer.PlayerId,
            Amount = -amount,
            Reason = reason,
            Counterparty = TransactionCounterparty.Player,
            CounterpartyPlayerId = player.PlayerId,
            CounterpartyPropertyIndex = counterpartyPropertyIndex
        });
    }


    // ───────────────────── Shortfall ─────────────────────

    /// <summary>
    /// Outcome of a shortfall-prompt round. Dictates whether the outer
    /// transaction should apply (FundsRaised) or be abandoned (DebtSettled,
    /// Bankrupted).
    /// </summary>
    private enum ShortfallOutcome
    {
        /// <summary>Loan / mortgage / sell-building gave the player enough cash; outer transaction continues.</summary>
        FundsRaised,

        /// <summary>A creditor deal discharged the debt; outer transaction must not also apply.</summary>
        DebtSettled,

        /// <summary>Player declared bankruptcy; outer transaction stops here.</summary>
        Bankrupted
    }

    /// <summary>
    /// Opens a <see cref="ShortfallPrompt"/> and dispatches the chosen action to the
    /// relevant sub-service.
    /// </summary>
    /// <remarks>
    /// Sub-services (LoanService, MortgageService, SellService, DealService,
    /// BankruptcyService) are not yet implemented — see the payment-service-pending
    /// project memory. The branches are TODO-stubbed and currently report the
    /// expected outcome shape so the outer <see cref="Move"/> logic can be wired
    /// against the final contract.
    /// </remarks>
    private async Task<ShortfallOutcome> ResolveShortfall(
        Framework.GameEngine engine,
        PlayerModel player,
        uint shortfallAmount,
        string? owedToPlayerId,
        ushort? counterpartyPropertyIndex,
        CancellationToken ct)
    {
        var remainingShortfall = shortfallAmount;
        while (remainingShortfall != 0)
        {
            var response = await engine.PromptProvider.RequestAsync(new ShortfallPrompt
            {
                PlayerId = player.PlayerId,
                Title = "You can't afford this",
                Body = $"You owe {RuleDictionary.Currency}{shortfallAmount} but only have {RuleDictionary.Currency}{player.Money}. Choose how to settle.",
                PlayerBalance = player.Money,
                Cost = shortfallAmount,
                OwedToPlayerId = owedToPlayerId
            }, ct);

            //TODO: These methods needs to modify remaining shortfall amount:
            //This is so we can keep prompting for shortfall until the player has raised enough.
            //original shortfallAmount variable kept for prompt, and final settlement - shortfallAmount = amount owed
            switch (response.Action)
            {
                case ShortfallAction.TakeLoan:
                    //TODO LoanService.TakeLoanForShortfall(engine, player, shortfallAmount, ct);
                    return ShortfallOutcome.FundsRaised;

                case ShortfallAction.Mortgage:
                    //TODO MortgageService.MortgageForShortfall(engine, player, shortfallAmount, ct);
                    return ShortfallOutcome.FundsRaised;

                case ShortfallAction.SellHouses:
                    //TODO PropertyService.SellBuildingsForShortfall(engine, player, shortfallAmount, ct);
                    return ShortfallOutcome.FundsRaised;

                case ShortfallAction.ProposeDeal:
                    //TODO DealService.ProposeSettlingDeal(engine, player, owedToPlayerId!, shortfallAmount, ct);
                    //  A creditor-deal that's accepted DISCHARGES the original debt — the
                    //  deal itself is the settlement (game-rules.md Default rule 7). Return
                    //  DebtSettled so the outer transaction does NOT apply.
                    //  A creditor-deal that's rejected re-opens the shortfall prompt; the
                    //  deal sub-service handles that loop internally before returning here.
                    return ShortfallOutcome.DebtSettled;

                case ShortfallAction.DeclareBankruptcy:
                    //TODO BankruptcyService.Declare(engine, player, owedToPlayerId, ct);
                    return ShortfallOutcome.Bankrupted;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(response.Action), response.Action, "Unhandled shortfall action.");
            }
        }

        return ShortfallOutcome.DebtSettled;
    }
}