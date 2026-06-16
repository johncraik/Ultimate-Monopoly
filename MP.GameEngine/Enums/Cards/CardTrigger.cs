namespace MP.GameEngine.Enums.Cards;

/// <summary>
/// The board/turn event(s) that make a held (keep-until-needed) card live to play —
/// the orthogonal axis to <see cref="CardConditionType"/> (how it is engaged). A
/// <c>[Flags]</c> set: most cards carry a single trigger, some several (ORed).
/// Resolve-on-draw cards (<see cref="CardConditionType.None"/>) carry <see cref="None"/>.
///
/// Derived from the real card inventory (<c>design-docs/cards-actions.md</c> §"Derived
/// triggers"; <c>cards-design.md</c> §5). Nearly every value lands on a branch the engine
/// already cites (<see cref="RuleCode"/>), so the held-card hook rides the existing
/// <c>CiteRule</c> points rather than a new event bus. The bracketed numbers are the
/// source action <c>No.</c>s in <c>cards-actions.md</c>.
/// </summary>
[Flags]
public enum CardTrigger
{
    None                    = 0,
    OnLandGo                = 1 << 0,   // land on GO (56, 110, 163)
    OnPassGo                = 1 << 1,   // pass GO — anti-clockwise variant is a condition parameter (102, 103)
    OnOtherPassGo           = 1 << 2,   // another player passes GO (97)
    OnLandFreeParking       = 1 << 3,   // land on Free Parking (58, 117)
    OnOtherTakesFreeParking = 1 << 4,   // another player takes the Free Parking money (109)
    OnRollDouble            = 1 << 5,   // roll a double (11, 114)
    OnRollTriple            = 1 << 6,   // roll a triple (10)
    OnOtherRollsTriple      = 1 << 7,   // another player rolls a triple / gains the triple bonus (12)
    OnEnterJail             = 1 << 8,   // sent to jail — arms a future effect (114, "next time in jail")
    OnInJail                = 1 << 9,   // while in jail, to leave (120, 122-125)
    OnPayPlayer             = 1 << 10,  // making a payment to another player (59)
    OnRentDue               = 1 << 11,  // rent is due / paid (112)
    OnNextRoll              = 1 << 12,  // after the holder's next roll (26)
    OnNextMove              = 1 << 13,  // after the holder's next move (70)
    OnCompleteSet           = 1 << 14,  // completing a colour set (79)
    OnTaxLanded             = 1 << 15,  // land on a tax space — the assessed tax is threaded as the trigger amount ("your next tax is tripled")
    OnSnakeEyes             = 1 << 16   // roll snake eyes (double 1) — the £500 bonus moment ("pay your snake-eyes money to the lowest roller")
}