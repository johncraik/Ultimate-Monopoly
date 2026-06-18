using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.Cards.Actions;

/// <summary>
/// Jail entry/exit driven by a card, resolved against <c>JailService</c>. Pure board
/// movement (e.g. "go back 3 spaces") is the separate <see cref="MovementAction"/>.
/// See <c>design-docs/cards-design.md</c> §3.
/// </summary>
public sealed class JailAction : CardAction
{
    public JailKind Kind { get; set; }

    /// <summary>Who it acts on (e.g. send self / a chosen player / all players to jail).</summary>
    public PlayerTarget Target { get; set; } = PlayerTarget.Self;

    /// <summary>
    /// Optional jail-term override for a <see cref="JailKind.SendToJail"/> (e.g. "go to jail
    /// for 10 turns" → <c>PlayerModel.MaxJailTurnsOverride</c>). Null = the default limit.
    /// </summary>
    public ushort? TurnsOverride { get; set; }

    /// <summary>
    /// For <see cref="JailKind.SendToJail"/>: the minimum turns the player must stay before they can
    /// leave ("…cannot leave jail" → <c>PlayerModel.MinJailTurns</c>). Null = no minimum (leave normally).
    /// </summary>
    public ushort? MinJailTurns { get; set; }

    /// <summary>
    /// For <see cref="JailKind.SendToJail"/>: the player keeps collecting rent while jailed
    /// ("…can collect all rent due" → <c>PlayerModel.CollectRentInJail</c>), overriding game-rules Default rule 2.
    /// </summary>
    public bool CollectRentInJail { get; set; }

    /// <summary>
    /// For <see cref="JailKind.ModifyLeaveFee"/>: set the player's jail-leave cost
    /// (<c>PlayerModel.JailCost</c>) to this exact amount (e.g. "reset to £50").
    /// </summary>
    public ushort? LeaveFeeSetTo { get; set; }

    /// <summary>
    /// For <see cref="JailKind.ModifyLeaveFee"/>: multiply the player's jail-leave cost
    /// (e.g. "tripled" → 3). Ignored when <see cref="LeaveFeeSetTo"/> is set.
    /// </summary>
    public ushort? LeaveFeeMultiplier { get; set; }

    /// <summary>
    /// For <see cref="JailKind.ModifyLeaveFee"/>: set the one-shot <c>PlayerModel.FreeNextJailExit</c> flag
    /// ("befriend a prison guard — next exit is free"). Leaves <c>JailCost</c> (and its escalation) intact;
    /// <c>PayJailFee</c> waives the next charge and clears the flag. Takes precedence over the set/multiply.
    /// </summary>
    public bool FreeNextExit { get; set; }
}