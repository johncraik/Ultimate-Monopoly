using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.EventReceipts;

/// <summary>
/// An immutable, atomic, after-the-fact record of one state change. The
/// engine emits one as a side-effect of every meaningful mutation; the
/// per-turn stream of receipts is the input to live UI narration and the
/// (future) statistical-snapshot projection. See
/// <c>design-docs/event-receipts.md</c>.
/// </summary>
/// <remarks>
/// Receipts are produced through <see cref="Abstractions.IEventEmitter"/>,
/// not by calling <see cref="GameCacheModel.AddEvent"/> directly. The base
/// fields below are populated as follows:
/// <list type="bullet">
/// <item><see cref="PlayerId"/> — set by the producer (rule service) at construction; the subject of the receipt.</item>
/// <item><see cref="TurnNumber"/> — assigned by <see cref="GameCacheModel.AddEvent"/> from the current turn metadata.</item>
/// <item><see cref="SequenceIndex"/> — assigned by <see cref="GameCacheModel.AddEvent"/> from the current per-turn event count, giving explicit ordering on serialised payloads.</item>
/// </list>
/// </remarks>
[JsonPolymorphic]
[JsonDerivedType(typeof(DiceRollReceipt), "DiceRoll")]
[JsonDerivedType(typeof(PlayerMovedReceipt), "PlayerMoved")]
[JsonDerivedType(typeof(PlayerDirectionChangedReceipt), "PlayerDirectionChanged")]
[JsonDerivedType(typeof(PlayerEnteredJailReceipt), "PlayerEnteredJail")]
[JsonDerivedType(typeof(PlayerSwappedReceipt), "PlayerSwapped")]
[JsonDerivedType(typeof(PlayerBankruptedReceipt), "PlayerBankrupted")]
[JsonDerivedType(typeof(CardTakenReceipt), "CardTaken")]
[JsonDerivedType(typeof(CardPlayedReceipt), "CardPlayed")]
[JsonDerivedType(typeof(FinancialTransactionReceipt), "FinancialTransaction")]
[JsonDerivedType(typeof(PropertyTransferReceipt), "PropertyTransfer")]
public abstract class EventReceipt
{
    /// <summary>The subject of the receipt — the player it is about. Producer sets this at construction.</summary>
    public string PlayerId { get; init; } = "";

    /// <summary>The turn this receipt was emitted within. Set by <see cref="GameCacheModel.AddEvent"/>.</summary>
    public uint TurnNumber { get; internal set; }

    /// <summary>Position within the per-turn event list at the moment of emission. Set by <see cref="GameCacheModel.AddEvent"/>.</summary>
    public ushort SequenceIndex { get; internal set; }
}