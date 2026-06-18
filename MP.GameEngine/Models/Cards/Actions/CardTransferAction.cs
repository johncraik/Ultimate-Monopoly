using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.Cards.Actions;

/// <summary>
/// Moves a held card between players' hands (cards-design.md §3, the Card category): <see cref="CardTransferKind.Pass"/>
/// gives one of the holder's chosen cards to a dice-off roller; <see cref="CardTransferKind.Steal"/> takes a chosen
/// card from a chosen player. The specific card is picked via a <c>CardOptionPrompt</c> over the relevant hand.
/// </summary>
public sealed class CardTransferAction : CardAction
{
    public CardTransferKind Kind { get; set; }

    /// <summary>The dice-off that picks the recipient for <see cref="CardTransferKind.Pass"/> (e.g. the highest one-die roller).</summary>
    public DiceOff? DiceOff { get; set; }
}
