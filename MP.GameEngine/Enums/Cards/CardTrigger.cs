namespace MP.GameEngine.Enums.Cards;

/// <summary>
/// The board/turn event(s) that make a held (keep-until-needed) card live to play —
/// the orthogonal axis to <see cref="CardConditionType"/> (how it is engaged). A
/// <c>[Flags]</c> set: most cards carry a single trigger, some several (ORed).
/// Resolve-on-draw cards (<see cref="CardConditionType.None"/>) carry <see cref="None"/>.
///
/// Derived from the held cards in the finalised list (<c>design-docs/cards.md</c>) — the
/// keep-until-needed cards that wait on a board/turn event (resolve-on-draw and "anytime on
/// your own turn" cards carry no trigger). Nearly every value lands on a branch the engine
/// already cites (<see cref="RuleCode"/>), so the held-card hook rides the existing
/// <c>CiteRule</c> points rather than a new event bus. Bit values are stable (gaps are retired
/// triggers) so persisted card JSON keeps deserialising.
/// </summary>
[Flags]
public enum CardTrigger
{
    None                    = 0,
    OnLandGo                = 1 << 0,   // land on GO — GO bonus threaded (pay-each-on-GO; GO money doubled ×5; no GO money ×5)
    OnPassGo                = 1 << 1,   // pass GO — anti-clockwise variant is a condition parameter (receive £X passing GO anti-clockwise)
    OnOtherPassGo           = 1 << 2,   // another player passes GO — a bystander steals their bonus (former prisoner)
    OnLandFreeParking       = 1 << 3,   // land on Free Parking (no cash on next FP visit; receive ALL the FP money)
    OnOtherTakesFreeParking = 1 << 4,   // another player takes the Free Parking money — a bystander receives it instead
    OnRollDouble            = 1 << 5,   // roll a double (convert double→triple; dodgy-judge double→triple while in jail)
    OnRollTriple            = 1 << 6,   // roll a triple (downgrade triple→double)
    OnOtherRollsTriple      = 1 << 7,   // another player rolls a triple — a bystander cancels their triple bonus
    // 1 << 8 retired (was OnEnterJail)
    OnInJail                = 1 << 9,   // while in jail (get-out-of-jail-free; befriend a guard → free next exit)
    // 1 << 10 retired (was OnPayPlayer — folded into OnRentDue)
    OnRentDue               = 1 << 11,  // paying rent to another player — "your next payment to another player is doubled"
    // 1 << 12 retired (was OnNextRoll — the real cards are OnNextMove)
    OnNextMove              = 1 << 13,  // after the holder's next move, roll or third-die (move forward 23 / back 17)
    // 1 << 14 retired (was OnCompleteSet)
    OnTaxLanded             = 1 << 15,  // land on a tax space — the assessed tax is threaded as the trigger amount ("your next tax is tripled")
    OnSnakeEyes             = 1 << 16,  // roll snake eyes (double 1) — the £500 bonus moment ("pay your snake-eyes money to the lowest roller")
    OnTurnStart             = 1 << 17,  // the holder's own turn begins, before the roll — the "anytime own turn" play window (advance X; change direction; …)
    OnSpaceLand             = 1 << 18   // the holder lands on a space (their move or another's third die) — the other "anytime own turn" window; pairs with OnTurnStart
}