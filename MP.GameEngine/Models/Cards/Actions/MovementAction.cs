using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.Cards.Actions;

/// <summary>
/// A board-position change driven by a card, resolved against <c>MovementService</c>.
/// Jail entry/exit is the separate <see cref="JailAction"/>. See
/// <c>design-docs/cards-design.md</c> §3.
/// </summary>
public sealed class MovementAction : CardAction
{
    public MovementKind Kind { get; set; }

    /// <summary>Signed spaces for <see cref="MovementKind.MoveSpaces"/> (+ forward, - back).</summary>
    public int Spaces { get; set; }

    /// <summary>Destination index for <see cref="MovementKind.AdvanceToIndex"/>.</summary>
    public ushort? TargetIndex { get; set; }

    /// <summary>Kind to seek for <see cref="MovementKind.AdvanceToNearest"/>.</summary>
    public NearestKind Nearest { get; set; }

    /// <summary>
    /// When true, an <see cref="MovementKind.AdvanceToNearest"/> seeks only a space owned by
    /// <i>another</i> player ("nearest station owned by someone else"). Default false = any.
    /// </summary>
    public bool NearestOwnedByOther { get; set; }

    /// <summary>Who moves.</summary>
    public PlayerTarget Target { get; set; } = PlayerTarget.Self;

    /// <summary>
    /// Filters the resolved <see cref="Target"/> players by jail state — e.g. mass breakout
    /// (<see cref="Enums.Cards.JailFilter.OnlyJailed"/>) or "call a meeting" (<see cref="Enums.Cards.JailFilter.OnlyNotJailed"/>).
    /// </summary>
    public JailFilter JailFilter { get; set; }

    /// <summary>"Do not pass GO" cards set this false to suppress the GO bonus when crossing.</summary>
    public bool CollectGoBonus { get; set; } = true;

    /// <summary>
    /// Whether the landed space's action is performed after the move. A swap does not
    /// (game-rules.md Movement rule 4).
    /// </summary>
    public bool ResolveLandedSpace { get; set; } = true;
}