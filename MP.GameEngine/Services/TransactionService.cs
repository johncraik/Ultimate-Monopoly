using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

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
        if(property == null) return;

        if (property.State != PropertyState.FreeParking && property.State != PropertyState.Owned)
            return; // mortgaged/reserved → no rent
        
        
        if (property.OwnerPlayerId is null && property.State != PropertyState.FreeParking) 
            return; // no owner and not in free parking    

        PlayerModel? owner = null;
        if(property.OwnerPlayerId is not null)
            owner = engine.Cache.Game.GetPlayer(property.OwnerPlayerId);
        
        if (owner is null && property.State != PropertyState.FreeParking) 
            return; // bankrupt owner; shouldn't own anything but defensive
        if (property.State != PropertyState.FreeParking && (owner?.IsInJail ?? true))
            return; // game-rules Default rule 2

        //Amount is passed in, computed/retrieved from call sight and not checked
        await Move(engine, player, -rent, FinancialReason.Rent,
            counterparty: property.State == PropertyState.FreeParking 
                ? TransactionCounterparty.FreeParking 
                : TransactionCounterparty.Player,
            counterpartyPlayer: property.State == PropertyState.FreeParking 
                ? null 
                : owner,
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
    /// The 20% mortgage fee paid each time a player passes GO with mortgaged
    /// properties (game-rules.md Mortgaging rule 1). Charged as one aggregate
    /// amount across all the player's mortgaged properties. Shortfall allowed.
    /// </summary>
    public Task PayMortgageFee(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.MortgageFee,
            counterparty: TransactionCounterparty.Bank,
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
    /// the card may pre-gate where required. <paramref name="reason"/> overrides the
    /// default <see cref="FinancialReason.CardCharge"/> so a context-amount card (e.g.
    /// "triple tax") records the true reason on the receipt and notification.
    /// </summary>
    public Task PayCardCharge(Framework.GameEngine engine, PlayerModel player, uint amount,
        TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer, CancellationToken ct,
        FinancialReason reason = FinancialReason.CardCharge)
        => Move(engine, player, -amount, reason,
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

    /// <summary>Triple-bonus payout — Bank → <paramref name="player"/>. The <paramref name="amount"/> is the
    /// (possibly modified / redirected) payout computed by <c>PlayerService.ApplyTripleBonus</c>.</summary>
    public Task ReceiveTripleBonus(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.TripleBonus,
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
        TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer, CancellationToken ct,
        FinancialReason reason = FinancialReason.CardPayout)
        => Move(engine, player, amount, reason,
            counterparty: counterparty,
            counterpartyPlayer: counterpartyPlayer,
            ct: ct);

    public Task ReceiveSnakeEyes(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
        => Move(engine, player, RuleDictionary.SnakeEyesBonus, FinancialReason.SneakEyes, 
            counterparty: TransactionCounterparty.Bank,
            ct: ct);
    
    public Task ReceiveFromBankruptPlayer(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
        => Move(engine, player, amount, FinancialReason.BankruptedPlayer,
            counterparty: TransactionCounterparty.Bank,
            ct: ct);
    

    // ───────────────────── Player-to-player ─────────────────────

    /// <summary>
    /// Money component of a player-to-player deal.
    /// Amount is taken from player and sent to counterparty
    /// </summary>
    public Task ProcessDealPayment(Framework.GameEngine engine, PlayerModel player, PlayerModel otherPlayer, long amount, CancellationToken ct)
        => Move(engine, player, -amount, FinancialReason.Deal,
            counterparty: TransactionCounterparty.Player,
            counterpartyPlayer: otherPlayer,
            allowShortfall: false,
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

                    var outcome = await engine.ShortfallService.ResolveShortfall(engine, player, debit,
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
        NotifyMovement(engine, player, amount, reason, counterparty, counterpartyPlayer, counterpartyPropertyIndex);

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

        if (counterparty != TransactionCounterparty.FreeParking)
            // Bank is untracked — no balance to mutate.
            return;
        
        // amount > 0: player took from FP → pot decreases (floored at 0 — the player still receives the
        // full amount, so a take exceeding the pot has the bank cover the shortfall, e.g. the "lucky day"
        // card; this also prevents the uint underflow a fixed over-pot take would otherwise cause).
        // amount < 0: player paid into FP → pot increases.
        engine.Cache.Game.FreeParkingAmount =
            (uint)Math.Max(0L, (long)engine.Cache.Game.FreeParkingAmount - amount);
            
        //Cite rule:
        engine.CiteRule(RuleCode.Default_FinesToFreeParking);
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

    /// <summary>
    /// Fires a lightweight, non-pausing notification toast for a just-applied money movement — one to
    /// the subject (their phone / the host drawer), and for a player-to-player move a mirrored one to
    /// the counterparty player (whose balance <see cref="ApplyBalances"/> moved too, with no Move of
    /// its own). Best-effort narration carrying as much detail as the move has: direction, amount,
    /// reason, the other side (the bank / Free Parking / another player), and the property name when
    /// one is involved. The engine holds no display names, so a player counterparty is generic.
    /// </summary>
    private static void NotifyMovement(
        Framework.GameEngine engine, PlayerModel player, long amount, FinancialReason reason,
        TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer, ushort? counterpartyPropertyIndex)
    {
        var propertyName = counterpartyPropertyIndex is { } index
            ? engine.Cache.Board.GetBoardSpace(index)?.Name
            : null;

        var absolute = (uint)Math.Abs(amount);

        // Subject perspective: amount > 0 = received, < 0 = paid.
        engine.Notifier.Notify(engine.Cache.GameId, player.PlayerId,
            BuildMovementMessage(amount > 0, absolute, reason, counterparty, propertyName));

        // The counterparty player sees the mirror — their counterparty is the subject (another player).
        if (counterpartyPlayer is not null)
            engine.Notifier.Notify(engine.Cache.GameId, counterpartyPlayer.PlayerId,
                BuildMovementMessage(amount < 0, absolute, reason, TransactionCounterparty.Player, propertyName));
    }

    /// <summary>Builds a "{Paid|Received} £X {to|from} {who}[ for {property}] ({reason})." narration line.</summary>
    private static string BuildMovementMessage(bool received, uint amount, FinancialReason reason,
        TransactionCounterparty counterparty, string? propertyName)
    {
        var verb = received ? "Received" : "Paid";
        var preposition = received ? "from" : "to";
        var who = counterparty switch
        {
            TransactionCounterparty.Player => "another player",
            TransactionCounterparty.FreeParking => "Free Parking",
            _ => "the bank"
        };
        var forProperty = string.IsNullOrWhiteSpace(propertyName) ? "" : $" for {propertyName}";
        return $"{verb} {RuleDictionary.Currency}{amount} {preposition} {who}{forProperty} ({ReasonText(reason)}).";
    }

    /// <summary>A short, player-facing phrase for each <see cref="FinancialReason"/> (the "why").</summary>
    private static string ReasonText(FinancialReason reason) => reason switch
    {
        FinancialReason.Rent => "rent",
        FinancialReason.Tax => "tax",
        FinancialReason.GoBonus => "GO bonus",
        FinancialReason.DiceNumBonus => "dice-number bonus",
        FinancialReason.SneakEyes => "snake eyes bonus",
        FinancialReason.TripleBonus => "triple bonus",
        FinancialReason.JailFee => "jail fee",
        FinancialReason.FreeParkingPay => "fee",
        FinancialReason.FreeParkingTake => "winnings",
        FinancialReason.LoanTake => "loan",
        FinancialReason.LoanRepay => "loan repayment",
        FinancialReason.Purchase => "property purchase",
        FinancialReason.UnReserve => "un-reserve",
        FinancialReason.Auction => "auction",
        FinancialReason.Build => "building",
        FinancialReason.Sell => "building sale",
        FinancialReason.Mortgage => "mortgage",
        FinancialReason.MortgageFee => "mortgage fee",
        FinancialReason.Unmortgage => "unmortgage",
        FinancialReason.CardPayout => "card",
        FinancialReason.CardCharge => "card",
        FinancialReason.Deal => "deal",
        FinancialReason.BankruptedPlayer => "bankrupt player",
        _ => reason.ToString()
    };
}