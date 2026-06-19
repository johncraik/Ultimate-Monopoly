using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Services.Cards;

/// <summary>
/// Evaluates held (keep-until-needed) cards at the engine's trigger points and drives the
/// prompt / force / apply / re-evaluate cycle. Mirrors <c>TransactionService</c>: a single private
/// core (<see cref="ProcessGameTrigger"/>) owns the algorithm, and <b>public methods — one per
/// held-card trigger</b> name the moment, thread the context that point supplies (the assessed tax,
/// the GO bonus, the Free Parking take, the rent, …) and call the core with the matching flag.
/// Self-documenting at the call site, exactly like the per-<c>FinancialReason</c> transaction methods.
///
/// In every method the subject argument (lander / roller / passer / payer / …) is the <b>subject</b>
/// of the trigger — whoever the event is happening to — never <c>engine.Cache.Game.CurrentPlayer()</c>
/// (under third-die board resolution the subject is not the turn player).
///
/// The trigger set is derived from the held cards in <c>design-docs/cards.md</c> (the resolve-on-draw
/// and "anytime on your own turn" cards need no trigger). Each method returns the aggregated
/// <see cref="SuppressDefault"/> across every card that fired on the trigger, so the call site knows
/// which part of its default to skip (the doubled tax replaces the charge, the GO-money card cancels
/// the bonus, …). A fresh aggregate is built per call — card definitions are never mutated.
/// </summary>
public class CardTriggerService
{
    // ───────────────────── GO ─────────────────────

    /// <summary>Subject landed on GO — the GO bonus is threaded as the trigger amount (GO money doubled,
    /// no money for landing on GO, pay each player on landing GO). <c>GoService.LandOnGo</c>.</summary>
    public Task<SuppressDefault> OnLandGo(Framework.GameEngine engine, PlayerModel lander, uint goBonus, CancellationToken ct)
        => ProcessGameTrigger(engine, lander, CardTrigger.OnLandGo, Ctx(goBonus, FinancialReason.GoBonus), ct);

    /// <summary>Subject passed GO — the pass bonus is the trigger amount. The anti-clockwise variant is a
    /// condition parameter checked during evaluation (receive £X passing GO anti-clockwise). <c>GoService.CollectGoMoney</c>.</summary>
    public Task<SuppressDefault> OnPassGo(Framework.GameEngine engine, PlayerModel passer, uint passBonus, CancellationToken ct)
        => ProcessGameTrigger(engine, passer, CardTrigger.OnPassGo, Ctx(passBonus, FinancialReason.GoBonus), ct);

    /// <summary>Another player passed GO — a bystander may steal their bonus (former prisoner). Subject is the
    /// passer; the holder reacts (any-player scope). <c>CollectGoMoney</c> during third-die movement.</summary>
    public Task<SuppressDefault> OnOtherPassGo(Framework.GameEngine engine, PlayerModel passer, uint passBonus, CancellationToken ct)
        => ProcessGameTrigger(engine, passer, CardTrigger.OnOtherPassGo, Ctx(passBonus, FinancialReason.GoBonus), ct);


    // ───────────────────── Free Parking ─────────────────────

    /// <summary>Subject landed on Free Parking — no-cash-next-visit (suppress) / receive-ALL-the-money (reads the
    /// pot directly), so no trigger amount. <c>FreeParkingService.ProcessFreeParking</c>.</summary>
    public Task<SuppressDefault> OnLandFreeParking(Framework.GameEngine engine, PlayerModel lander, CancellationToken ct)
        => ProcessGameTrigger(engine, lander, CardTrigger.OnLandFreeParking, null, ct);

    /// <summary>Another player took the Free Parking money — a bystander may receive it instead. Subject is the
    /// taker; the take amount is the trigger amount.</summary>
    public Task<SuppressDefault> OnOtherTakesFreeParking(Framework.GameEngine engine, PlayerModel taker, uint amount, CancellationToken ct)
        => ProcessGameTrigger(engine, taker, CardTrigger.OnOtherTakesFreeParking, Ctx(amount, FinancialReason.FreeParkingTake), ct);


    // ───────────────────── Dice ─────────────────────

    /// <summary>Subject rolled a double — convert double→triple; the dodgy-judge double→triple (gated on an
    /// in-jail condition). Orchestrator double branch.</summary>
    public Task<SuppressDefault> OnRollDouble(Framework.GameEngine engine, PlayerModel roller, CancellationToken ct)
        => ProcessGameTrigger(engine, roller, CardTrigger.OnRollDouble, null, ct);

    /// <summary>Subject rolled a triple — downgrade triple→double. Orchestrator triple branch.</summary>
    public Task<SuppressDefault> OnRollTriple(Framework.GameEngine engine, PlayerModel roller, CancellationToken ct)
        => ProcessGameTrigger(engine, roller, CardTrigger.OnRollTriple, null, ct);

    /// <summary>Another player rolled a triple — a bystander may cancel their triple bonus. Subject is the roller.</summary>
    public Task<SuppressDefault> OnOtherRollsTriple(Framework.GameEngine engine, PlayerModel roller, CancellationToken ct)
        => ProcessGameTrigger(engine, roller, CardTrigger.OnOtherRollsTriple, null, ct);

    /// <summary>Subject rolled snake eyes (double 1) — the £500 bonus moment ("pay your snake-eyes money to the
    /// lowest roller"). The bonus is threaded as the trigger amount.</summary>
    public Task<SuppressDefault> OnSnakeEyes(Framework.GameEngine engine, PlayerModel roller, CancellationToken ct)
        => ProcessGameTrigger(engine, roller, CardTrigger.OnSnakeEyes, Ctx(RuleDictionary.SnakeEyesBonus, FinancialReason.SneakEyes), ct);


    // ───────────────────── Jail ─────────────────────

    /// <summary>Subject is in jail and may play a card (get-out-of-jail-free; befriend a guard → next exit is free).
    /// The leave-jail path.</summary>
    public Task<SuppressDefault> OnInJail(Framework.GameEngine engine, PlayerModel jailed, CancellationToken ct)
        => ProcessGameTrigger(engine, jailed, CardTrigger.OnInJail, null, ct);


    // ───────────────────── Rent ─────────────────────

    /// <summary>Subject is paying rent to another player — "your next payment to another player is doubled". The
    /// rent is the trigger amount and <paramref name="ownerId"/> the player being paid (threaded so the held
    /// card can pay that same owner an equal extra). <c>PropertyService.PayPropertyRent</c>.</summary>
    public Task<SuppressDefault> OnPayRent(Framework.GameEngine engine, PlayerModel payer, uint rent, string? ownerId, CancellationToken ct)
        => ProcessGameTrigger(engine, payer, CardTrigger.OnRentDue, Ctx(rent, FinancialReason.Rent, ownerId), ct);


    // ───────────────────── Movement ─────────────────────

    /// <summary>After the subject's next move — roll <i>or</i> third-die movement ("after your next move, move
    /// forward 23 / go back 17"). Post-move, <c>MovementService</c>.</summary>
    public Task<SuppressDefault> OnNextMove(Framework.GameEngine engine, PlayerModel mover, CancellationToken ct)
        => ProcessGameTrigger(engine, mover, CardTrigger.OnNextMove, null, ct);


    // ───────────────────── Tax ─────────────────────

    /// <summary>Subject landed on a tax space — the keystone of the held-tax modifiers. The assessed tax is
    /// threaded as the trigger amount ("your next tax is tripled"). <c>TaxService.PayTax</c>.</summary>
    public Task<SuppressDefault> OnTaxLanded(Framework.GameEngine engine, PlayerModel lander, uint taxAmount, CancellationToken ct)
        => ProcessGameTrigger(engine, lander, CardTrigger.OnTaxLanded, Ctx(taxAmount, FinancialReason.Tax), ct);


    // ───────────────────── Anytime own turn ─────────────────────

    /// <summary>The subject landed on a space — the other "anytime own turn" window, fired after every move
    /// (the subject's own roll/double/triple move and when moved by another player's third die — the holder
    /// is still the subject being moved, cards-design.md §4.1).</summary>
    public Task<SuppressDefault> OnSpaceLand(Framework.GameEngine engine, PlayerModel lander, CancellationToken ct)
        => ProcessGameTrigger(engine, lander, CardTrigger.OnSpaceLand, null, ct);


    // ───────────────────── Core ─────────────────────

    /// <summary>Builds the trigger context an amount-carrying trigger threads into the played card's actions
    /// (the <c>AmountSource.TriggerAmount</c> seam). Amountless triggers pass <c>null</c>.</summary>
    private static CardActionContext Ctx(long amount, FinancialReason reason, string? counterpartyId = null)
        => new() { TriggerAmount = amount, TriggerReason = reason, TriggerCounterpartyId = counterpartyId };

    /// <summary>
    /// The shared evaluation core every public trigger method funnels through (mirrors
    /// <c>TransactionService.Move</c>). Scans the right hands for cards live on <paramref name="trigger"/>,
    /// prompts/forces in turn order, plays the chosen card with <paramref name="context"/>, then
    /// re-evaluates so a state change (e.g. triple→double) lets other players' cards react.
    /// </summary>
    private async Task<SuppressDefault> ProcessGameTrigger(Framework.GameEngine engine, PlayerModel subject, CardTrigger trigger,
        CardActionContext? context, CancellationToken ct)
        //NOTE: How this should conceptually work:
        // - Called from a sub-system at a trigger point (like land on go).
        // - This then needs to search for any cards in any players where the trigger matches (contains flag)
        // - Matching players with a valid card are then filtered out depending on player context (subject passed in):
        // Such that the correct condition is met (CardHolderTurn = subject -- AnyPlayerTurn = ignore subject context, dont filter out)
        // - For each of the players with playable cards, we go in clockwise order prompting if they wish to play one of their cards;
        // we show a single option list of the valid cards that can be played on this trigger based on the condition
        // - This will need a new prompt: where only 1 card, simple yes/no dialogue - where >1, radio choice (like other choice prompts)
        // - Upon receiving a yes/chosen card, we play that card (threading context) - then we RE-EVALUATE THE TRIGGER
        // - Why re-evaluate the trigger? Because the card played may have changed the state of the game (like downgrade triple to double),
        // other cards from other players then need to revaluate against the new state - to ensure those cards are valid.
        // This allows chaining of cards in this trigger from different players (if condition is met).
        // - Cards in the subject context with FORCED play are always played first (in the order they were added to the player, which is default list order)
        // - Forced cards take precedence before choice cards
        // - GUARD: a card already fired in THIS resolution pass must be excluded from re-evaluation, else a
        //   forced multi-use card (TurnsRemaining > 0, stays in hand) re-matches and loops forever.
        // - Return type becomes a typed result (granular suppression) once that layer lands.
        => await EvaluatePlayableCards(engine, trigger, subject, context, ct: ct);


    private async Task<SuppressDefault> EvaluatePlayableCards(Framework.GameEngine engine, CardTrigger trigger, PlayerModel subject,
        CardActionContext? context, HashSet<HeldCard>? playedCards = null, SuppressDefault? completeSuppress = null, CancellationToken ct = default)
    {
        //Get cards that have a matching trigger:
        var matchingCards = MatchingCardForTrigger(engine, subject, trigger);
        if (matchingCards.Count == 0)
            return completeSuppress ?? new SuppressDefault(SuppressDefaultType.None);
        
        //Filter cards to remove already played cards
        //(cant play the same card twice, such as recurring cards: e.g., valid for 5 turns etc)
        if(playedCards != null)
            //Cant play 2 cards from same player
            matchingCards = matchingCards
                .Where(c => !playedCards.Select(pc => pc.Card.CardId).Contains(c.Card.CardId) 
                            && !playedCards.Select(pc => pc.Player.PlayerId).Contains(c.Player.PlayerId))
                .ToList();
        
        //Filter the cards based on the subject context:
        var filteredCards = FilterToContext(matchingCards, subject);
        if (filteredCards.Count == 0)
            return completeSuppress ?? new SuppressDefault(SuppressDefaultType.None);
        
        //Prompt players to pick a card to play:
        var playedCard = await PromptPlayers(engine, subject.PlayerId, filteredCards, ct);
        if (playedCard == null)
            return completeSuppress ?? new SuppressDefault(SuppressDefaultType.None);
        
        //Plays the card:
        var suppressDefaults = await engine.CardService.PlayCard(engine, playedCard.Player, playedCard.Card, ct, context);
        if (completeSuppress != null)
            completeSuppress.Aggregate(suppressDefaults);
        else
            completeSuppress = new SuppressDefault(suppressDefaults.Type());
        
        playedCards ??= [];
        playedCards.Add(playedCard);
        return await EvaluatePlayableCards(engine, trigger, subject, context, playedCards, completeSuppress, ct);
    }


    #region Card Filtering

    private record HeldCard(PlayerModel Player, CardModel Card);
    
    private List<HeldCard> MatchingCardForTrigger(Framework.GameEngine engine, PlayerModel subject, CardTrigger trigger)
    {
        var matchingCards = new List<HeldCard>();
        foreach (var held in from player in engine.Cache.Game.GetPlayers(subject.PlayerId, excludePovPlayer: false)
                 //Jailed players can't play cards through the trigger pipeline — the only in-jail play is the
                 //OnInJail trigger (get-out-of-jail-free / befriend a guard, on their own jailed subject).
                 //Exclude jailed holders from every other trigger (anytime cards, bystander reactions).
                 where !player.IsInJail || trigger == CardTrigger.OnInJail
                 let mc = player.Cards
                     .Where(c =>
                     {
                         if (c.ConditionType == CardConditionType.None)
                             return false;

                         //"After your NEXT move": a card drawn THIS turn must not fire on the move that drew
                         //it (the JustVisiting "go back 17"/"forward 23" cards are drawn on landing, then the
                         //same move's OnNextMove fires immediately). DrawnOnTurn is stamped on draw; only let
                         //it fire once the turn number has advanced. Constant during this evaluation, so the
                         //re-evaluation recursion can't sneak it back in.
                         if (trigger == CardTrigger.OnNextMove
                             && c.DrawnOnTurn is { } drawnTurn
                             && engine.Cache.Game.Metadata.TurnNumber <= drawnTurn)
                             return false;

                         //Live when any condition matches the trigger flag AND its gates (if any): the
                         //direction gate (e.g. "passing GO anti-clockwise") and the jail-state gate
                         //(e.g. "a double in jail becomes a triple") against the subject.
                         return c.Conditions.Any(cd => cd.Trigger.HasFlag(trigger)
                             && (cd.RequiredDirection is null || cd.RequiredDirection == subject.Direction)
                             && cd.JailFilter switch
                             {
                                 JailFilter.OnlyJailed => subject.IsInJail,
                                 JailFilter.OnlyNotJailed => !subject.IsInJail,
                                 _ => true
                             });
                     })
                     .ToList()
                 where mc.Count != 0
                 select mc.Select(c => new HeldCard(player, c)).ToList())
        {
            matchingCards.AddRange(held);
        }

        return matchingCards;
    }

    private List<HeldCard> FilterToContext(List<HeldCard> matchingCards, PlayerModel subject)
        => matchingCards.Where(c =>
            {
                var holderPlayerId = c.Player.PlayerId;
                if (holderPlayerId == subject.PlayerId)
                    //The holder IS the subject — only their own-turn (cardholder) cards apply. An any-player
                    //card is a bystander reaction to an event happening to SOMEONE ELSE ("steal the GO bonus
                    //another player collected", "receive the Free Parking money another took"); it must never
                    //fire on its own holder — you can't steal your own GO bonus when you pass GO.
                    return c.Card.ConditionType is CardConditionType.MetCardholderTurn
                        or CardConditionType.ChoiceCardholderTurn;

                //Only any is valid (since holder is not the subject)
                return c.Card.ConditionType is CardConditionType.MetAnyPlayerTurn
                    or CardConditionType.ChoiceAnyPlayerTurn;
            }).ToList();

    #endregion



    #region Card Pick Prompting

    private async Task<HeldCard?> PromptPlayers(Framework.GameEngine engine, string subjectId, List<HeldCard> allCards, CancellationToken ct)
    {
        var allPlayers = engine.Cache.Game.GetPlayers(subjectId, excludePovPlayer: false);
        foreach (var p in allPlayers)
        {
            //Get valid cards the player has:
            var cards = allCards.Where(c => c.Player.PlayerId == p.PlayerId).ToList();
            if (cards.Count == 0)
                continue;
            
            var forcedCard = cards.FirstOrDefault(c => c.Card.ConditionType 
                is CardConditionType.MetCardholderTurn 
                or CardConditionType.MetAnyPlayerTurn);
            if (forcedCard != null)
            {
                _ = await engine.PromptProvider.Acknowledge(p.PlayerId, $"Playing Held {forcedCard.Card.CardType.ToDisplayName()} Card",
                    forcedCard.Card.GetDisplayText(engine.Cache, p.PlayerId), timeout: TimeSpan.FromSeconds(30), 
                    cardType: forcedCard.Card.CardType, ct: ct);
                return forcedCard;
            }
            
            //Prompt player to pick a card
            var card = await PromptForCard(engine, p.PlayerId, cards, ct);
            if (card != null)
                return card;
        }
        
        //No card was picked
        return null;
    }
    
    private async Task<HeldCard?> PromptForCard(Framework.GameEngine engine, string playerId, List<HeldCard> cards, CancellationToken ct)
    {
        var options = cards.Select(c => 
            new CardOption(c.Card.CardId, c.Card.GetDisplayText(engine.Cache, c.Player.PlayerId)))
            .ToList();
        
        var body = options.Count > 1
            ? "Do you want to play one of the following cards?"
            : "Do you want to play this card?";
        var response = await engine.PromptProvider.RequestAsync(new CardOptionPrompt
        {
            PlayerId = playerId,
            Title = "Want to play a card?",
            Body = body,
            Options = options,
            PlayCardChoice = true
        }, ct: ct);
        
        if(string.IsNullOrEmpty(response.SelectedKey))
            return null;
        
        var cardId = response.SelectedKey;
        var card = cards.FirstOrDefault(c => c.Card.CardId == cardId);
        return card;
    }
    

    #endregion
}