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
    private readonly ICardActionService<TurnsAction> _turnsActionService;
    private readonly ICardActionService<DirectionAction> _directionActionService;
    private readonly ICardActionService<LoansAction> _loansActionService;
    private readonly ICardActionService<BuildingAction> _buildingActionService;
    private readonly ICardActionService<PropertyAction> _propertyActionService;
    private readonly ICardActionService<GlobalEventAction> _globalEventActionService;
    private readonly ICardActionService<DeckDrawAction> _deckDrawActionService;
    private readonly ICardActionService<DiceAction> _diceActionService;
    private readonly ICardActionService<NoOpAction> _noOpActionService;
    private readonly ICardActionService<CardTransferAction> _cardTransferActionService;

    /// <summary>
    /// Creates the card interpreter over the per-action handlers it dispatches to
    /// (one <see cref="ICardActionService{T}"/> per concrete <see cref="CardAction"/> type).
    /// </summary>
    public CardService(ICardActionService<MoneyAction> moneyActionService,
        ICardActionService<MovementAction> movementActionService,
        ICardActionService<JailAction> jailActionService,
        ICardActionService<TurnsAction> turnsActionService,
        ICardActionService<DirectionAction> directionActionService,
        ICardActionService<LoansAction> loansActionService,
        ICardActionService<BuildingAction> buildingActionService,
        ICardActionService<PropertyAction> propertyActionService,
        ICardActionService<GlobalEventAction> globalEventActionService,
        ICardActionService<DeckDrawAction> deckDrawActionService,
        ICardActionService<DiceAction> diceActionService,
        ICardActionService<NoOpAction> noOpActionService,
        ICardActionService<CardTransferAction> cardTransferActionService)
    {
        _moneyActionService = moneyActionService;
        _movementActionService = movementActionService;
        _jailActionService = jailActionService;
        _turnsActionService = turnsActionService;
        _directionActionService = directionActionService;
        _loansActionService = loansActionService;
        _buildingActionService = buildingActionService;
        _propertyActionService = propertyActionService;
        _globalEventActionService = globalEventActionService;
        _deckDrawActionService = deckDrawActionService;
        _diceActionService = diceActionService;
        _noOpActionService = noOpActionService;
        _cardTransferActionService = cardTransferActionService;
    }


    /// <summary>
    /// Draws the next card of <paramref name="type"/> for <paramref name="player"/> and
    /// either resolves it immediately (resolve-on-draw, <see cref="CardConditionType.None"/>)
    /// or adds it to the player's hand (keep-until-needed). Resolved cards return to the back
    /// of the deck. No-op on an empty deck. See cards-design.md §4 (interaction modes), §9 (decks).
    /// <paramref name="context"/> is the optional override-on-draw context (e.g. a Tax space threading
    /// the assessed tax as the trigger amount) — read by a resolve-on-draw card's <c>TriggerAmount</c>
    /// money source; null for every draw that supplies no such figure.
    /// </summary>
    public async Task<SuppressDefault> DrawCard(Framework.GameEngine engine, PlayerModel player, CardType type, CancellationToken ct, CardActionContext? context = null)
    {
        var card = engine.Cache.Game.CardDecks.Take(type);
        if (card is null)
            //Empty deck — nothing to draw.
            return new SuppressDefault(SuppressDefaultType.None);

        //Always show card picked up — carry the CardType so the front end can flavour the
        //acknowledge by deck (every other acknowledge passes null → default secondary styling).
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, $"{card.CardType.ToDisplayName()} Card",
            card.GetDisplayText(engine.Cache, player.PlayerId), timeout: TimeSpan.FromSeconds(30), cardType: card.CardType, ct: ct);
        
        if (!card.IsKeepUntilNeeded)
        {
            //Resolve-on-draw (override-on-draw, §4b): apply now, then return to the deck. A
            //resolve-on-draw card always cycles back regardless of the apply result.
            _ = await ResolveCard(engine, player, card, ct, context);
            ReturnToDeck(engine, card);
            return card.SuppressDefault;
        }

        //Keep-until-needed — held in the player's hand until its trigger fires. A kept card does NOT
        //suppress the drawing space's default (card-triggers.md §11.1): its SuppressDefault applies only
        //when it is later played / triggered (PlayCard returns it then). E.g. "no GO money for the next 5
        //landings" must NOT cancel the £200 on the very landing that drew it — the holder gets it this turn.
        player.Cards.Add(card);
        engine.EventEmitter.Emit(new CardTakenReceipt { PlayerId = player.PlayerId, CardType = card.CardType });
        return new SuppressDefault(SuppressDefaultType.None);
    }
    
    /// <summary>
    /// Plays a keep-until-needed card the player already holds (mode (a), cards-design.md §4):
    /// resolves its effect (which emits the <see cref="CardPlayedReceipt"/>), then removes it from
    /// the player's hand and returns it to the back of its deck (§9.4). This is the single seam every
    /// hand-played card funnels through — the forced jail exit, the turn-start "use card" command,
    /// and (later) the trigger-fired held-card hook and the NOPE/immunity counter window.
    /// <paramref name="card"/> is the instance held in <see cref="PlayerModel.Cards"/> (matched by reference).
    /// <paramref name="context"/> is the optional trigger context (e.g. the amount that fired the card) —
    /// the trigger layer supplies it; the manual jail-exit / use-card plays pass <c>null</c>.
    /// </summary>
    public async Task<SuppressDefault> PlayCard(Framework.GameEngine engine, PlayerModel player, CardModel card, CancellationToken ct, CardActionContext? context = null)
    {
        //Remove the card from the hand BEFORE resolving its actions, so it cannot re-match its own
        //trigger while those actions run. Without this an "advance N" card loops forever: its move
        //re-resolves the landed space (ResolveLandedSpace), which re-fires OnSpaceLand, which finds the
        //still-held card and plays it again. Re-added below if the play is rejected or it's multi-use.
        player.Cards.Remove(card);

        var applied = await ResolveCard(engine, player, card, ct, context);
        var chosenGroup = card.Groups.FirstOrDefault(g => g.IsChosenGroup);

        if (!applied)
        {
            //The play didn't take effect (e.g. a jail release blocked by a card lock) — return the card
            //to the player's hand, untouched, so they can try again. Undo the chosen-group mark.
            if (chosenGroup is not null)
                chosenGroup.IsChosenGroup = false;
            //Re-add at index 0: cards are drawn from the front, so a returned card goes back to the front.
            player.Cards.Insert(0, card);
            return new SuppressDefault(SuppressDefaultType.None);
        }

        if(chosenGroup is null)
            throw new InvalidOperationException("Played a card that doesn't have a chosen group.");

        if(chosenGroup.TurnsRemaining is > 1)
        {
            //Still can be played/activated again later — decrement and return it to the hand at index 0, so it
            //stays the first match on the next trigger (keeps firing until spent, rather than flip-flopping with
            //a sibling multi-use card that was appended after it).
            chosenGroup.TurnsRemaining--;
            player.Cards.Insert(0, card);
            return card.SuppressDefault;
        }

        //Reset chosen group and turns remaining
        foreach (var g in card.Groups)
        {
            g.IsChosenGroup = false;
            g.TurnsRemaining = g.TurnsActive;
        }

        //Spent — already removed from the hand above; return it to the back of its deck.
        ReturnToDeck(engine, card);
        return card.SuppressDefault;
    }


    /// <summary>
    /// Resolves a card: selects the group to apply (a single group applies directly; multiple
    /// groups are an OR-choice surfaced via <see cref="CardOptionPrompt"/>), then applies that
    /// group's actions in order (ANDed). Emits a <see cref="CardPlayedReceipt"/> — a resolve-on-draw
    /// card still counts as played even though it never reaches the hand.
    /// </summary>
    private async Task<bool> ResolveCard(Framework.GameEngine engine, PlayerModel player, CardModel card, CancellationToken ct, CardActionContext? context = null)
    {
        if (card.Groups.Count == 0)
            //Nothing to apply.
            return true;
        
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

        //Actions within the chosen group are ANDed — every one runs (non-short-circuit &), and the
        //result is whether the play took effect. False (only a blocked jail release today) tells
        //PlayCard to retain the card in hand rather than consume it.
        var applied = true;
        foreach (var action in group.Actions)
            applied &= await ApplyAction(engine, player, action, ct, context);

        //A play that didn't take effect isn't "played" — no receipt, and PlayCard keeps the card.
        if (applied)
            engine.EventEmitter.Emit(new CardPlayedReceipt { PlayerId = player.PlayerId, CardType = card.CardType });

        return applied;
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
    private Task<bool> ApplyAction(Framework.GameEngine engine, PlayerModel player, CardAction action, CancellationToken ct, CardActionContext? context = null)
        => action switch
        {
            MoneyAction m     => _moneyActionService.ResolveActionAsync(engine, player, m, ct, context),
            MovementAction v  => _movementActionService.ResolveActionAsync(engine, player, v, ct, context),
            JailAction j      => _jailActionService.ResolveActionAsync(engine, player, j, ct, context),
            TurnsAction t     => _turnsActionService.ResolveActionAsync(engine, player, t, ct, context),
            DirectionAction d => _directionActionService.ResolveActionAsync(engine, player, d, ct, context),
            LoansAction l     => _loansActionService.ResolveActionAsync(engine, player, l, ct, context),
            BuildingAction b  => _buildingActionService.ResolveActionAsync(engine, player, b, ct, context),
            PropertyAction p  => _propertyActionService.ResolveActionAsync(engine, player, p, ct, context),
            GlobalEventAction g => _globalEventActionService.ResolveActionAsync(engine, player, g, ct, context),
            DeckDrawAction dd => _deckDrawActionService.ResolveActionAsync(engine, player, dd, ct, context),
            DiceAction di => _diceActionService.ResolveActionAsync(engine, player, di, ct, context),
            NoOpAction n => _noOpActionService.ResolveActionAsync(engine, player, n, ct, context),
            CardTransferAction ctr => _cardTransferActionService.ResolveActionAsync(engine, player, ctr, ct, context),
            ImmunityAction i => Task.FromResult(true),  //Immunity cards (immunity action) always resolve - handled separately in CardImmunityService
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unhandled card action type.")
        };
    
}