using System.Text.Json.Serialization;
using MP.GameEngine.Models.Cards.Actions;

namespace MP.GameEngine.Models.Cards;

/// <summary>
/// Base type for a single card effect — the closed action vocabulary (cards-design.md §3). Each
/// concrete subclass carries its own parameters and is resolved by its
/// <see cref="Abstractions.Cards.ICardActionService{T}"/> handler.
/// </summary>
// Cards (and their actions) persist in the game snapshot, so actions serialise
// polymorphically by discriminator — same pattern as EventReceipt / Prompt. Each
// new action type adds a [JsonDerivedType] line.
[JsonPolymorphic]
[JsonDerivedType(typeof(MoneyAction), "Money")]
[JsonDerivedType(typeof(MovementAction), "Movement")]
[JsonDerivedType(typeof(JailAction), "Jail")]
[JsonDerivedType(typeof(TurnsAction), "Turns")]
[JsonDerivedType(typeof(DirectionAction), "Direction")]
[JsonDerivedType(typeof(LoansAction), "Loans")]
[JsonDerivedType(typeof(BuildingAction), "Building")]
[JsonDerivedType(typeof(PropertyAction), "Property")]
[JsonDerivedType(typeof(GlobalEventAction), "GlobalEvent")]
[JsonDerivedType(typeof(DeckDrawAction), "DeckDraw")]
[JsonDerivedType(typeof(DiceAction), "Dice")]
[JsonDerivedType(typeof(NoOpAction), "NoOp")]
[JsonDerivedType(typeof(CardTransferAction), "CardTransfer")]
public abstract class CardAction
{
    /// <summary>Stable identity (GUID), shared with the persisted card definition on re-import.</summary>
    public string ActionId { get; set; }
}