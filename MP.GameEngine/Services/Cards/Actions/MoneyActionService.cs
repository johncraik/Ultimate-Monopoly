using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="MoneyAction"/> — a money movement realised from the holder's
/// per-unit count, an optional <see cref="DiceMultiplier"/> (a fresh holder roll), and the
/// percentage-card %cap, then routed to its counterparty: the Bank / Free Parking, every other
/// active player (<see cref="MoneyCounterparty.EachPlayer"/>), or a dice-off winner
/// (<see cref="MoneyCounterparty.DiceOffPlayer"/>, configured by the action's <see cref="MoneyAction.DiceOff"/>).
/// Every player-to-player movement runs from the <b>payer's</b> POV, so each payer's affordability
/// (and any shortfall) is its own — never the receiver's (transactions.md §4). See cards-design.md §3.
/// </summary>
public class MoneyActionService : ICardActionService<MoneyAction>
{
    private readonly TransactionService _transactionService;
    private readonly DiceService _diceService;
    private readonly CardImmunityService _immunityService;

    /// <summary>Creates the money-action handler over the transaction seam and the dice seam it routes through.</summary>
    public MoneyActionService(TransactionService transactionService, 
        DiceService diceService,
        CardImmunityService immunityService)
    {
        _transactionService = transactionService;
        _diceService = diceService;
        _immunityService = immunityService;
    }

    /// <summary>
    /// Rolls the dice multiplier (if any), realises the amount, and applies it to the action's
    /// counterparty. No-ops on a zero realised amount (e.g. a per-house charge with no buildings).
    /// </summary>
    /// <param name="engine">The game engine bundle the money movement mutates.</param>
    /// <param name="player">The card holder paying or receiving.</param>
    /// <param name="action">The money action to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, MoneyAction action, CancellationToken ct, CardActionContext? context = null)
    {
        // Swap-all-cash short-circuits the amount entirely.
        if (action.SwapCash)
        {
            await SwapCash(engine, player, action, ct);
            return true;
        }

        // The dice-off winner is the subject ("the lowest roller pays the tax"). Resolved here via the
        // dice service and stashed on the context, so a later action in the group (a Swap) can target the
        // same player. No winner (empty pool) → no-op.
        if (action.Target == PlayerTarget.DiceOffPlayer)
        {
            await ApplyToDiceOffSubject(engine, player, action, context, ct);
            return true;
        }

        // The shared context player a prior action in this group stashed (a swap partner / dice-off winner)
        // is the subject — no fresh prompt/roll. Used by the GO swap's "both players receive £200".
        if (action.Target == PlayerTarget.ContextPlayer)
        {
            await ApplyToContextSubject(engine, player, action, context, ct);
            return true;
        }

        // Self (default): the holder is the subject, with the counterparty driving where it flows.
        if (action.Target == PlayerTarget.Self)
        {
            // Issue #16: a jailed holder cannot roll dice for a card. Every dice-multiplier card is a
            // landing-on-a-space reward (GO / Chance / Community Chest / Tax) paired with a movement the
            // jail guard already blocks (e.g. "advance to GO, then roll 2 dice × £100" — the advance
            // no-ops in jail), so the dice-roll part must no-op too rather than paying the holder a
            // phantom bonus. Non-dice Self money still resolves in jail (a flat grant/charge is fine).
            // Holder-only: a multi-target "all players roll" only ever fires from a non-jailed holder
            // who landed on the space, so the per-other-player paths are deliberately left alone.
            if (player.IsInJail && action.DiceMultiplier != DiceMultiplier.None)
                return true;

            // A dice multiplier is a fresh roll by the holder, folded into the realised amount.
            var diceMultiplier = await RollDiceMultiplier(engine, player, action.DiceMultiplier, ct);
            var amount = RealiseAmount(engine, player, action, diceMultiplier, context);
            if (amount == 0)
                return true;
            await ApplyByCounterparty(engine, player, action, amount, ResolveReason(action, context), context, ct);
            return true;
        }

        // Multi-target: each targeted player is the subject of a Bank / Free Parking move (a grant or
        // a charge) — e.g. "each other player receives £200 × the two dice they roll". The amount is
        // realised per subject (so per-unit / %cap follow the affected player) and, crucially, the
        // dice multiplier is a fresh roll by THAT subject — the prompt and the figure both belong to
        // the player actually receiving/paying, never the card holder. (No multiplier → no prompt.)
        foreach (var subject in await CardActionHelper.ResolveTargets(engine, player, action.Target, ct))
        {
            var diceMultiplier = await RollDiceMultiplier(engine, subject, action.DiceMultiplier, ct);
            var amount = RealiseAmount(engine, subject, action, diceMultiplier, context);
            if (amount == 0)
                continue;
            await ApplyToBankOrFreeParking(engine, subject, action.Direction, action.Counterparty, amount, ResolveReason(action, context), ct);
        }

        return true;
    }

    /// <summary>Applies a realised amount for the holder per the action's counterparty (the original Self-path routing).</summary>
    private Task ApplyByCounterparty(Framework.GameEngine engine, PlayerModel holder, MoneyAction action, uint amount, FinancialReason reason, CardActionContext? context, CancellationToken ct)
        => action.Counterparty switch
        {
            MoneyCounterparty.EachPlayer => ApplyToEachPlayer(engine, holder, action.Direction, amount, ct),
            MoneyCounterparty.DiceOffPlayer => ApplyToDiceOffWinner(engine, holder, action.Direction, action.DiceOff, amount, ct),
            MoneyCounterparty.TriggerPlayer => ApplyToTriggerPlayer(engine, holder, action.Direction, amount, reason, context, ct),
            _ => ApplyToBankOrFreeParking(engine, holder, action.Direction, action.Counterparty, amount, reason, ct)
        };

    /// <summary>
    /// The player the firing trigger supplied (<see cref="CardActionContext.TriggerCounterpartyId"/>) is the
    /// counterparty — the holder pays them (or receives from them, payer-POV). Used by "your next payment to
    /// another player is doubled": the held card pays an equal extra to the same owner it owes rent to. No-op
    /// when no trigger counterparty was supplied, the named player is gone, or it resolves to the holder.
    /// </summary>
    private async Task ApplyToTriggerPlayer(Framework.GameEngine engine, PlayerModel holder,
        MoneyDirection direction, uint amount, FinancialReason reason, CardActionContext? context, CancellationToken ct)
    {
        if (context?.TriggerCounterpartyId is not { } otherId)
            return;

        var other = engine.Cache.Game.GetPlayer(otherId);
        if (other is null || other.PlayerId == holder.PlayerId)
            return;

        // Payer-POV (transactions.md §4); carry the trigger reason (e.g. Rent) so the receipt/notification reads correctly.
        if (direction == MoneyDirection.Pay)
            await _transactionService.PayCardCharge(engine, holder, amount, TransactionCounterparty.Player, other, ct, reason);
        else
            await _transactionService.PayCardCharge(engine, other, amount, TransactionCounterparty.Player, holder, ct, reason);
    }

    /// <summary>
    /// The financial reason a Bank / Free Parking card move records: a <see cref="AmountSource.TriggerAmount"/>
    /// action carries the firing trigger's reason (tax, GO bonus, …) so the receipt and toast read correctly;
    /// every other action is a plain card payout / charge.
    /// </summary>
    private static FinancialReason ResolveReason(MoneyAction action, CardActionContext? context)
        => action.AmountSource == AmountSource.TriggerAmount && context is not null
            ? context.TriggerReason
            : action.Direction == MoneyDirection.Receive ? FinancialReason.CardPayout : FinancialReason.CardCharge;

    /// <summary>
    /// Resolves the action's dice-off to a single subject (the lowest/highest roller), records them on the
    /// context so a later action in the group acts on the same player (card 444's Swap), and applies the
    /// realised amount as that subject's Bank / Free Parking move ("the lowest roller pays the tax"). No-op
    /// when the dice-off has no winner (empty pool).
    /// </summary>
    private async Task ApplyToDiceOffSubject(Framework.GameEngine engine, PlayerModel holder, MoneyAction action,
        CardActionContext? context, CancellationToken ct)
    {
        if (action.DiceOff is null)
            return;

        var subject = await _diceService.ResolveDiceOffTarget(engine, holder, action.DiceOff, ct);
        if (subject is null)
            return;

        // Share the resolved player so a following action (the Swap in card 444) acts on the same one.
        if (context is not null)
            context.ContextPlayerId = subject.PlayerId;

        var diceMultiplier = await RollDiceMultiplier(engine, subject, action.DiceMultiplier, ct);
        var amount = RealiseAmount(engine, subject, action, diceMultiplier, context);
        if (amount == 0)
            return;

        await ApplyToBankOrFreeParking(engine, subject, action.Direction, action.Counterparty, amount,
            ResolveReason(action, context), ct);
    }

    /// <summary>
    /// The shared context player (<see cref="CardActionContext.ContextPlayerId"/>, stashed by an earlier
    /// action in the group — a swap partner or dice-off winner) is the subject of a Bank / Free Parking
    /// move, applied with no fresh prompt/roll. Used by the GO swap's "both players receive £200" (the Swap
    /// stashes the chosen partner; this grants them). No-op when nothing was stashed or the player is gone.
    /// </summary>
    private async Task ApplyToContextSubject(Framework.GameEngine engine, PlayerModel holder, MoneyAction action,
        CardActionContext? context, CancellationToken ct)
    {
        if (context?.ContextPlayerId is not { } id)
            return;

        var subject = engine.Cache.Game.GetPlayer(id);
        if (subject is null)
            return;

        var diceMultiplier = await RollDiceMultiplier(engine, subject, action.DiceMultiplier, ct);
        var amount = RealiseAmount(engine, subject, action, diceMultiplier, context);
        if (amount == 0)
            return;

        await ApplyToBankOrFreeParking(engine, subject, action.Direction, action.Counterparty, amount,
            ResolveReason(action, context), ct);
    }

    /// <summary>
    /// Swaps the holder's entire cash with a counterparty player — a chosen player, or the
    /// highest/lowest one-die dice-off roller. No-op when there is no valid counterparty.
    /// </summary>
    private async Task SwapCash(Framework.GameEngine engine, PlayerModel holder, MoneyAction action, CancellationToken ct)
    {
        PlayerModel? target;
        if (action.Counterparty == MoneyCounterparty.DiceOffPlayer)
        {
            target = action.DiceOff is null
                ? null
                : await _diceService.ResolveDiceOffTarget(engine, holder, action.DiceOff, ct);
        }
        else
        {
            target = (await CardActionHelper.ResolveTargets(engine, holder, PlayerTarget.ChosenPlayer, ct)).FirstOrDefault();
        }

        if (target is null || target.PlayerId == holder.PlayerId)
            return;
        
        var result = await _immunityService.CheckSwappingMoneyImmunity(engine, target, ct);
        if (result)
        {
            engine.Notifier.Notify(engine.Cache.GameId, holder.PlayerId, 
                "Player played an immunity card. You do not swap your money");
            engine.Notifier.Notify(engine.Cache.GameId, target.PlayerId, 
                "You played an immunity card. You do not swap your money");
            return;
        }

        (holder.Money, target.Money) = (target.Money, holder.Money);
    }


    /// <summary>Bank / Free Parking — the holder simply pays or receives the amount (no shortfall on a receive).</summary>
    private async Task ApplyToBankOrFreeParking(Framework.GameEngine engine, PlayerModel holder,
        MoneyDirection direction, MoneyCounterparty counterparty, uint amount, FinancialReason reason, CancellationToken ct)
    {
        var target = counterparty == MoneyCounterparty.FreeParking
            ? TransactionCounterparty.FreeParking
            : TransactionCounterparty.Bank;

        if (direction == MoneyDirection.Receive)
            await _transactionService.ReceiveCardPayout(engine, holder, amount, target, null, ct, reason);
        else
            await _transactionService.PayCardCharge(engine, holder, amount, target, null, ct, reason);
    }


    /// <summary>
    /// Every other active player. Always driven from the payer's POV (transactions.md §4): when the
    /// holder PAYS, the holder is the payer in each move; when the holder RECEIVES (e.g. "collect £50
    /// from each player"), each other player is the payer — so a payer who can't afford it gets their
    /// own shortfall, and a bankrupt one doesn't break the others.
    /// </summary>
    private async Task ApplyToEachPlayer(Framework.GameEngine engine, PlayerModel holder,
        MoneyDirection direction, uint amount, CancellationToken ct)
    {
        foreach (var other in engine.Cache.Game.GetPlayers(holder.PlayerId))
        {
            if (direction == MoneyDirection.Pay)
                await _transactionService.PayCardCharge(engine, holder, amount, TransactionCounterparty.Player, other, ct);
            else
                await _transactionService.PayCardCharge(engine, other, amount, TransactionCounterparty.Player, holder, ct);
        }
    }


    /// <summary>
    /// The dice-off winner (per the action's <see cref="DiceOff"/>) is the single counterparty; the
    /// holder then pays them (or receives from them, payer-POV). No-op when the dice-off has no winner,
    /// or the holder wins their own dice-off (the movement would be with themselves).
    /// </summary>
    private async Task ApplyToDiceOffWinner(Framework.GameEngine engine, PlayerModel holder,
        MoneyDirection direction, DiceOff? diceOff, uint amount, CancellationToken ct)
    {
        if (diceOff is null)
            return;

        var winner = await _diceService.ResolveDiceOffTarget(engine, holder, diceOff, ct);
        if (winner is null || winner.PlayerId == holder.PlayerId)
            return;

        if (direction == MoneyDirection.Pay)
            await _transactionService.PayCardCharge(engine, holder, amount, TransactionCounterparty.Player, winner, ct);
        else
            await _transactionService.PayCardCharge(engine, winner, amount, TransactionCounterparty.Player, holder, ct);
    }


    /// <summary>
    /// The holder's dice-multiplier roll summed (1 or 2 dice); 1 when the action has no multiplier.
    /// <see cref="DiceMultiplier.TwoDiceByThirdDie"/> additionally multiplies the fresh two-dice total by
    /// the third die already rolled this turn ("roll 2 dice × the third die rolled this turn").
    /// </summary>
    private async Task<uint> RollDiceMultiplier(Framework.GameEngine engine, PlayerModel holder,
        DiceMultiplier multiplier, CancellationToken ct)
    {
        if (multiplier == DiceMultiplier.None)
            return 1;

        var count = multiplier is DiceMultiplier.TwoDice or DiceMultiplier.TwoDiceByThirdDie ? (ushort)2 : (ushort)1;
        var roll = await _diceService.RollCardDice(engine, holder, count,
            "Roll the dice", "Roll the dice to determine the card amount.", ct);

        var total = count >= 2 ? (uint)(roll.Die1 + (roll.Die2 ?? 0)) : roll.Die1;

        if (multiplier == DiceMultiplier.TwoDiceByThirdDie)
        {
            //Compound: the fresh two-dice total × the third die already rolled this turn.
            var thirdDie = engine.Cache.GetTurnDiceRoll()?.ThirdDie
                ?? throw new InvalidOperationException("No turn third die to multiply the card amount by.");
            total *= thirdDie;
        }

        return total;
    }


    /// <summary>
    /// Base amount scaled by the per-unit count (houses/hotels/properties owned), the
    /// <paramref name="diceMultiplier"/> (a pre-rolled holder total), and — for percentage cards —
    /// the player's %cap (100/50/10), in that order. Returns 0 for a genuinely-zero realised amount.
    /// <paramref name="context"/> carries the trigger amount the <see cref="AmountSource.TriggerAmount"/>
    /// base reads.
    /// </summary>
    private static uint RealiseAmount(Framework.GameEngine engine, PlayerModel player, MoneyAction action, uint diceMultiplier, CardActionContext? context)
    {
        // The base figure. AmountSource.TriggerAmount overrides Basis: the base is the figure the firing
        // trigger supplied (the tax just assessed, the FP take, …) and Amount is reused as the factor
        // (×2 "double", ×1 "exactly that"). No context — a resolve-on-draw play, or a play with no
        // trigger amount — floors to 0, a silent no-op. Otherwise it's a fixed amount, a fraction of
        // cash / the FP pot, the triple bonus, or the snake-eyes bonus (Amount is the percent for the
        // percentage bases).
        decimal amount = action.AmountSource == AmountSource.TriggerAmount
            ? (context?.TriggerAmount ?? 0) * action.Amount
            : action.Basis switch
            {
                MoneyAmountBasis.PercentOfOwnCash => (long)player.Money * action.Amount / 100,
                MoneyAmountBasis.PercentOfFreeParkingPot => (long)engine.Cache.Game.FreeParkingAmount * action.Amount / 100,
                MoneyAmountBasis.TripleBonus => player.TripleBonus,
                MoneyAmountBasis.SnakeEyesBonus => RuleDictionary.SnakeEyesBonus,
                _ => action.Amount
            };

        var (houses, hotels) = engine.Cache.Game.GetHousesAndHotelsTaken(player.PlayerId);
        amount *= action.PerUnit switch
        {
            MoneyPerUnit.PerHouse => houses,
            MoneyPerUnit.PerHotel => hotels,
            MoneyPerUnit.PerProperty => engine.Cache.Game.GetOwnedProperties(player.PlayerId).Count,
            _ => 1
        };

        amount *= diceMultiplier;

        if (action.PercentageApplies)
            amount = (amount * engine.Cache.Game.PlayerPercentCap(player.PlayerId)) / 100;

        // A genuinely-zero realised amount — a per-house/per-hotel charge with no buildings, or a
        // percentage that floors to nothing — is "nothing to pay". Short-circuit BEFORE NormaliseAmount,
        // whose never-round-to-zero rule would otherwise bump it to the grid minimum. For a 0 input that
        // bump is the NEGATIVE grid minimum (positive = amount > 0 is false at 0), which the (uint) cast
        // then wraps into a huge charge. Returning 0 here also lets the caller's `amount == 0` guard fire.
        if (amount <= 0)
            return 0;

        return (uint)MoneyHelper.NormaliseAmount((long)Math.Round(amount, MidpointRounding.AwayFromZero), engine.Cache.RoundingRule,
            action.Direction == MoneyDirection.Receive
                ? FinancialReason.CardPayout
                : FinancialReason.CardCharge);
    }
}