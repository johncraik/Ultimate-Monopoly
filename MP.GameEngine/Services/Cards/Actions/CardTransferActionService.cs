using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Snapshot.Cards;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="CardTransferAction"/> — moving a held card between players' hands
/// (cards-design.md §3, the Card category): <see cref="CardTransferKind.Pass"/> (the holder gives one of
/// their held cards to a dice-off roller) or <see cref="CardTransferKind.Steal"/> (the holder takes a
/// chosen card from a chosen player). The chooser picks the specific card via a mandatory
/// <see cref="CardOptionPrompt"/> over the relevant hand. No-op when there is nothing to move.
/// </summary>
public class CardTransferActionService : ICardActionService<CardTransferAction>
{
    private readonly DiceService _diceService;

    /// <summary>Creates the card-transfer handler over the dice seam its Pass dice-off rolls through.</summary>
    public CardTransferActionService(DiceService diceService)
    {
        _diceService = diceService;
    }

    /// <summary>Dispatches to the pass / steal flow. Always returns true (a no-op transfer still "applied").</summary>
    public async Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, CardTransferAction action, CancellationToken ct, CardActionContext? context = null)
    {
        switch (action.Kind)
        {
            case CardTransferKind.Pass:
                await Pass(engine, player, action, ct);
                break;
            case CardTransferKind.Steal:
                await Steal(engine, player, ct);
                break;
        }

        return true;
    }

    /// <summary>
    /// The holder gives one of their held cards (their choice) to the dice-off winner. The recipient is
    /// resolved first (the roll-off), then the holder picks which card to hand over. No-op when the holder
    /// holds no cards, the action has no dice-off, or the dice-off yields no other player.
    /// </summary>
    private async Task Pass(Framework.GameEngine engine, PlayerModel holder, CardTransferAction action, CancellationToken ct)
    {
        if (holder.Cards.Count == 0 || action.DiceOff is null)
            return;

        var recipient = await _diceService.ResolveDiceOffTarget(engine, holder, action.DiceOff, ct);
        if (recipient is null || recipient.PlayerId == holder.PlayerId)
            return;

        var card = await PickCard(engine, holder.PlayerId, holder.Cards, "Pass a card", "Choose one of your cards to pass.", ct);
        if (card is null)
            return;

        holder.Cards.Remove(card);
        recipient.Cards.Add(card);
    }

    /// <summary>
    /// The holder takes a chosen card from a chosen player. No-op when no player is chosen, it resolves to
    /// the holder, or that player holds no cards.
    /// </summary>
    private async Task Steal(Framework.GameEngine engine, PlayerModel holder, CancellationToken ct)
    {
        var target = (await CardActionHelper.ResolveTargets(engine, holder, PlayerTarget.ChosenPlayer, ct)).FirstOrDefault();
        if (target is null || target.PlayerId == holder.PlayerId || target.Cards.Count == 0)
            return;

        var card = await PickCard(engine, holder.PlayerId, target.Cards, "Steal a card", "Choose a card to steal.", ct);
        if (card is null)
            return;

        target.Cards.Remove(card);
        holder.Cards.Add(card);
    }

    /// <summary>
    /// A mandatory card pick from <paramref name="cards"/> via the existing <see cref="CardOptionPrompt"/>
    /// (the group-choice mode — not the play-or-decline one), options keyed by <c>CardId</c> and labelled
    /// with the card text. Returns null only on an empty / forged response.
    /// </summary>
    private static async Task<CardModel?> PickCard(Framework.GameEngine engine, string chooserId,
        IReadOnlyList<CardModel> cards, string title, string body, CancellationToken ct)
    {
        var options = cards
            .Select(c => new CardOption(c.CardId, c.GetDisplayText(engine.Cache, chooserId)))
            .ToList();

        var response = await engine.PromptProvider.RequestAsync(new CardOptionPrompt
        {
            PlayerId = chooserId,
            Title = title,
            Body = body,
            Options = options
        }, ct);

        return cards.FirstOrDefault(c => c.CardId == response.SelectedKey);
    }
}
