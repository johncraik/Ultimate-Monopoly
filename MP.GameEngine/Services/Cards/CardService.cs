using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Services.Cards;

/// <summary>
/// The card interpreter — draws cards from the per-type decks and resolves their
/// data-driven actions against the existing engine services. The card model is pure
/// data (cards-design.md §3); this service owns the draw/resolve/choose-group flow and
/// dispatches each action to its typed <see cref="ICardActionService{T}"/> handler, so the
/// per-action behaviour lives in one small service per action type rather than a monolith.
/// </summary>
public class CardService
{
    private readonly ICardActionService<MoneyAction> _moneyActionService;
    private readonly ICardActionService<MovementAction> _movementActionService;
    private readonly ICardActionService<JailAction> _jailActionService;

    /// <summary>
    /// Creates the card interpreter over the per-action handlers it dispatches to
    /// (one <see cref="ICardActionService{T}"/> per concrete <see cref="CardAction"/> type).
    /// </summary>
    public CardService(ICardActionService<MoneyAction> moneyActionService,
        ICardActionService<MovementAction> movementActionService,
        ICardActionService<JailAction> jailActionService)
    {
        _moneyActionService = moneyActionService;
        _movementActionService = movementActionService;
        _jailActionService = jailActionService;
    }


    /// <summary>
    /// Draws the next card of <paramref name="type"/> for <paramref name="player"/> and
    /// either resolves it immediately (resolve-on-draw, <see cref="CardConditionType.None"/>)
    /// or adds it to the player's hand (keep-until-needed). Resolved cards return to the back
    /// of the deck. No-op on an empty deck. See cards-design.md §4 (interaction modes), §9 (decks).
    /// </summary>
    public async Task<bool> DrawCard(Framework.GameEngine engine, PlayerModel player, CardType type, CancellationToken ct)
    {
        var card = engine.Cache.Game.CardDecks.Take(type);
        if (card is null)
            //Empty deck — nothing to draw.
            return false;

        //Always show card picked up:
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, $"{card.CardType.ToDisplayName()} Card",
            card.GetDisplayText(engine.Cache, player.PlayerId), timeout: TimeSpan.FromSeconds(30), ct: ct);
        
        if (!card.IsKeepUntilNeeded)
        {
            //Resolve-on-draw (override-on-draw, §4b): apply now, then return to the deck.
            await ResolveCard(engine, player, card, ct);
            ReturnToDeck(engine, card);
            return card.SuppressDefault;
        }

        //Keep-until-needed — held in the player's hand until its trigger fires (held-card
        //trigger evaluation is a later increment).
        player.Cards.Add(card);
        engine.EventEmitter.Emit(new CardTakenReceipt { PlayerId = player.PlayerId, CardType = card.CardType });
        return card.SuppressDefault;
    }
    
    /// <summary>
    /// Plays a keep-until-needed card the player already holds (mode (a), cards-design.md §4):
    /// resolves its effect (which emits the <see cref="CardPlayedReceipt"/>), then removes it from
    /// the player's hand and returns it to the back of its deck (§9.4). This is the single seam every
    /// hand-played card funnels through — the forced jail exit, the turn-start "use card" command,
    /// and (later) the trigger-fired held-card hook and the NOPE/immunity counter window.
    /// <paramref name="card"/> is the instance held in <see cref="PlayerModel.Cards"/> (matched by reference).
    /// </summary>
    public async Task PlayCard(Framework.GameEngine engine, PlayerModel player, CardModel card, CancellationToken ct)
    {
        await ResolveCard(engine, player, card, ct);
        var chosenGroup = card.Groups.FirstOrDefault(g => g.IsChosenGroup);
        if(chosenGroup is null)
            throw new InvalidOperationException("Played a card that doesn't have a chosen group.");
        
        if(chosenGroup.TurnsRemaining is > 0)
            //Still can be played/activated again later
            return;
        
        //Reset chosen group and turns remaining
        foreach (var g in card.Groups)
        {
            g.IsChosenGroup = false;
            g.TurnsRemaining = g.TurnsActive; 
        }
        
        player.Cards.Remove(card);
        ReturnToDeck(engine, card);
    }


    /// <summary>
    /// Resolves a card: selects the group to apply (a single group applies directly; multiple
    /// groups are an OR-choice surfaced via <see cref="CardOptionPrompt"/>), then applies that
    /// group's actions in order (ANDed). Emits a <see cref="CardPlayedReceipt"/> — a resolve-on-draw
    /// card still counts as played even though it never reaches the hand.
    /// </summary>
    private async Task ResolveCard(Framework.GameEngine engine, PlayerModel player, CardModel card, CancellationToken ct)
    {
        if (card.Groups.Count == 0)
            //Nothing to apply.
            return;
        
        CardGroup group;
        if (card.Groups.Count == 1)
        {
            //Single group — no choice to make.
            group = card.Groups[0];
        }
        else
        {
            //Multiple groups = a choice (cards-design.md §2). Options are keyed by the stable
            //GroupId (keys-not-indexes), labelled with the group's text.
            var response = await engine.PromptProvider.RequestAsync(new CardOptionPrompt
            {
                PlayerId = player.PlayerId,
                Title = "Choose an option",
                Body = card.GetDisplayText(engine.Cache, player.PlayerId),
                Options = card.Groups.Select(g => 
                    new CardOption(g.GroupId, g.GetDisplayText(engine.Cache, player.PlayerId)))
                    .ToList()
            }, ct);

            group = card.Groups.First(g => g.GroupId == response.SelectedKey);
        }

        group.IsChosenGroup = true;
        
        //Actions within the chosen group are ANDed — applied in order.
        foreach (var action in group.Actions)
            await ApplyAction(engine, player, action, ct);

        engine.EventEmitter.Emit(new CardPlayedReceipt { PlayerId = player.PlayerId, CardType = card.CardType });
    }
    


    /// <summary>Returns a spent/played card to the back of its type's deck (cards-design.md §9.4).</summary>
    private static void ReturnToDeck(Framework.GameEngine engine, CardModel card)
        => engine.Cache.Game.CardDecks.HandBack(card.CardType, card);


    /// <summary>
    /// Dispatches a single <see cref="CardAction"/> to its typed
    /// <see cref="ICardActionService{T}"/> handler — the interpreter seam (cards-design.md §3).
    /// Each handler owns its action type's behaviour (the per-unit/%cap realisation, the
    /// nearest-finder, the swap, …). A new action type adds a <see cref="CardAction"/> subclass,
    /// a handler service, and one arm here.
    /// </summary>
    private Task ApplyAction(Framework.GameEngine engine, PlayerModel player, CardAction action, CancellationToken ct)
        => action switch
        {
            MoneyAction m    => _moneyActionService.ResolveActionAsync(engine, player, m, ct),
            MovementAction v => _movementActionService.ResolveActionAsync(engine, player, v, ct),
            JailAction j     => _jailActionService.ResolveActionAsync(engine, player, j, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unhandled card action type.")
        };
    
}