using MP.GameEngine.Enums;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services;

/// <summary>
/// Owns every property ownership change in the engine — a title moving between a
/// player, the bank, or the Free Parking pot. The property-side mirror of
/// <see cref="TransactionService"/>: each public method maps to a single
/// <see cref="PropertyTransferReason"/> for readable call sites and a consistent
/// receipt-emission surface.
///
/// The service does <i>only</i> title: it flips ownership/state on the
/// <see cref="PropertyModel"/> and emits the <see cref="PropertyTransferReceipt"/>.
/// It does <b>not</b> move money (that is <see cref="TransactionService"/>'s job,
/// via its own receipt), recompute rent levels, or fire downstream rule effects
/// (the reserve-rule break-through, etc.). Those are the caller's job and happen
/// <i>after</i> the relevant transfer method returns — mirroring
/// <see cref="TransactionService"/>'s money-only contract.
///
/// Sign convention everywhere here matches <c>PropertyTransferReceipt.Value</c>:
/// positive = the subject player acquired the property, negative = relinquished
/// it. Each public method names the direction so call sites never think about signs.
///
/// Like <see cref="TransactionService"/>, mutations land on the cache working copy
/// and are not committed here — promotion happens only at the turn-state boundary.
/// </summary>
public class PropertyTransferService
{
    // ───────────────────── Acquire (the player GAINS a title) ─────────────────────

    /// <summary>Outright purchase from the bank. Caller handles the money leg (<c>PurchaseProperty</c>) first.</summary>
    public void Buy(Framework.GameEngine engine, PlayerModel player, PropertyModel property)
        => Move(engine, player, property, +1, PropertyTransferReason.Buy,
            counterparty: TransactionCounterparty.Bank);

    /// <summary>
    /// Reserving the set-completing property from the bank (game-rules.md Reserved
    /// Properties). The player owns it but in the Reserved state. Caller handles the
    /// half-price money leg first.
    /// </summary>
    public void Reserve(Framework.GameEngine engine, PlayerModel player, PropertyModel property)
        => Move(engine, player, property, +1, PropertyTransferReason.Reserved,
            counterparty: TransactionCounterparty.Bank);

    /// <summary>Winning a property at auction. Caller handles the <c>WinAuction</c> money leg first.</summary>
    public void WinAtAuction(Framework.GameEngine engine, PlayerModel player, PropertyModel property)
        => Move(engine, player, property, +1, PropertyTransferReason.Auction,
            counterparty: TransactionCounterparty.Bank);

    /// <summary>Taking a property out of the Free Parking pot — the player gains it and it becomes Owned.</summary>
    public void TakeFromFreeParking(Framework.GameEngine engine, PlayerModel player, PropertyModel property)
        => Move(engine, player, property, +1, PropertyTransferReason.FreeParking,
            counterparty: TransactionCounterparty.FreeParking);

    
    
    // ───────────────────── Relinquish (the player LOSES a title) ─────────────────────

    /// <summary>Handing a property into the Free Parking pot (game-rules.md Free Parking).</summary>
    public void HandIntoFreeParking(Framework.GameEngine engine, PlayerModel player, PropertyModel property)
        => Move(engine, player, property, -1, PropertyTransferReason.FreeParking,
            counterparty: TransactionCounterparty.FreeParking);

    /// <summary>
    /// Returning a single property to the bank — a card effect or a cancelled
    /// reservation. For a full bankruptcy use <see cref="Bankrupt"/>.
    /// </summary>
    public void ReturnToBank(Framework.GameEngine engine, PlayerModel player, PropertyModel property)
        => Move(engine, player, property, -1, PropertyTransferReason.ReturnToBank,
            counterparty: TransactionCounterparty.Bank);

    
    
    // ───────────────────── Player-to-player ─────────────────────

    /// <summary>
    /// A property changing hands in a deal or by a card. Driven from the
    /// <paramref name="to"/> player's perspective — they acquire it (+1) and
    /// <paramref name="from"/> relinquishes it (-1, mirrored receipt). The title is
    /// reassigned via <see cref="PropertyModel.OwnProperty"/>, which preserves a
    /// mortgaged/reserved state across the transfer.
    /// </summary>
    public void Transfer(Framework.GameEngine engine, PlayerModel from, PlayerModel to, PropertyModel property)
        => Move(engine, to, property, +1, PropertyTransferReason.Deal,
            counterparty: TransactionCounterparty.Player, counterpartyPlayer: from);

    
    
    // ───────────────────── Bankruptcy (bulk) ─────────────────────

    /// <summary>
    /// Returns every property the player owns to the bank on bankruptcy
    /// (game-rules.md Bankruptcy rule 2 — assets never go to another player), including
    /// mortgaged and reserved ones. One receipt per property, all carrying
    /// <see cref="PropertyTransferReason.Bankrupt"/>.
    /// </summary>
    public void Bankrupt(Framework.GameEngine engine, PlayerModel player)
    {
        var owned = engine.Cache.Game.GetOwnedProperties(player.PlayerId);
        foreach (var property in owned)
            Move(engine, player, property, -1, PropertyTransferReason.Bankrupt,
                counterparty: TransactionCounterparty.Bank);
    }


    // ───────────────────── Core helper ─────────────────────

    /// <summary>
    /// Applies a single property's ownership change for <paramref name="player"/> and
    /// emits the receipt(s). <paramref name="value"/> is signed from the player's
    /// perspective: +1 = acquired, -1 = relinquished. The Bank is untracked (no pot
    /// to mutate) and the Free Parking pot holds no count, so — unlike
    /// <see cref="TransactionService"/> — there is no counterparty balance to adjust;
    /// the property's own state carries the move.
    /// </summary>
    private void Move(
        Framework.GameEngine engine,
        PlayerModel player,
        PropertyModel property,
        int value,
        PropertyTransferReason reason,
        TransactionCounterparty counterparty = TransactionCounterparty.Bank,
        PlayerModel? counterpartyPlayer = null)
    {
        if (value == 0) return;

        ApplyOwnership(player, property, value, reason, counterparty);
        EmitReceipts(engine, player, value, reason, counterparty, counterpartyPlayer);
    }

    /// <summary>
    /// Flips the title on the property. An acquire (<paramref name="value"/> &gt; 0)
    /// hands ownership to the player via <see cref="PropertyModel.OwnProperty"/>
    /// (which keeps a mortgaged/reserved state on a player→player transfer and becomes
    /// Owned from the bank or Free Parking); a <see cref="PropertyTransferReason.Reserved"/>
    /// acquire then flips it to the Reserved state. A relinquish
    /// (<paramref name="value"/> &lt; 0) sends the title to its destination — the bank
    /// or the Free Parking pot. A player→player relinquish needs no state change of its
    /// own: the receiving player's acquire reassigns the title (deals are driven from
    /// the receiver's POV — see <see cref="Transfer"/>).
    /// </summary>
    private static void ApplyOwnership(PlayerModel player, PropertyModel property, int value,
        PropertyTransferReason reason, TransactionCounterparty counterparty)
    {
        if (value > 0)
        {
            property.OwnProperty(player.PlayerId);
            if (reason == PropertyTransferReason.Reserved)
                property.ReserveProperty();
            return;
        }

        switch (counterparty)
        {
            case TransactionCounterparty.Bank:
                property.ReturnToBank();
                break;
            case TransactionCounterparty.FreeParking:
                property.HandInToFreeParking();
                break;
            case TransactionCounterparty.Player:
            default:
                // Receiver-driven (see Transfer) — no giver-side state change; the
                // receiver's acquire reassigns the title.
                break;
        }
    }

    /// <summary>
    /// Emits the <see cref="PropertyTransferReceipt"/> from the subject player's
    /// perspective, plus a mirrored receipt (opposite sign, mirrored counterparty)
    /// from the counterparty player's perspective when the move is player-to-player.
    /// Mirrors <see cref="TransactionService"/>'s two-perspective emission.
    /// </summary>
    private static void EmitReceipts(
        Framework.GameEngine engine, PlayerModel player, int value, PropertyTransferReason reason,
        TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer)
    {
        // Subject player's perspective.
        engine.EventEmitter.Emit(new PropertyTransferReceipt
        {
            PlayerId = player.PlayerId,
            Value = value,
            SetsOnly = false,
            Reason = reason,
            Counterparty = counterparty,
            CounterpartyPlayerId = counterpartyPlayer?.PlayerId
        });

        if (counterpartyPlayer is null) return;

        // Counterparty player's perspective — opposite sign, mirrored counterparty.
        engine.EventEmitter.Emit(new PropertyTransferReceipt
        {
            PlayerId = counterpartyPlayer.PlayerId,
            Value = -value,
            SetsOnly = false,
            Reason = reason,
            Counterparty = TransactionCounterparty.Player,
            CounterpartyPlayerId = player.PlayerId
        });
    }
}