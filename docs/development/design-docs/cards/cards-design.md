# Cards — Architecture & Design

The architecture doc for the card sub-system — the cards counterpart to
`game-engine.md`. It defines *how* cards are modelled and resolved; the per-card
**contents** (every card, its text, its actions) get their own authoritative doc,
`card-decks.md` (`game-engine.md` §11.5), written off the model this doc settles.
The raw action/condition inventory lives in
`config/Monopoly Cards and Types.xlsx`.

**Status:** the card sub-system described here is substantially **built** (see
`cards.md` and `cards-dev-changes.md` for the per-card / per-mechanic state, and
the drift note at the foot of this document). The framework pieces it leans on
already exist:

| Need | Existing piece |
|---|---|
| Group choice ("pay £200 OR draw a card") | `CardOptionPrompt` (`choice-events.md` §15.9) — keyed n-ary choice |
| Counter window (NOPE / immunity) | `InterruptibleWindowPrompt` (`choice-events.md` §9) — **but the engine has drifted from it; needs a realignment pass (§6)** |
| Global-event cards | `GlobalEventService` / `EventInfo` / `GlobalEvent` |
| Percentage-card tiers | `GameModel.PlayerPercentCap` (100 / 50 / 10) |
| Trigger points | the `RuleCode` / `CiteRule` branch map (§5) |
| Card type | `Enums/Cards/CardType.cs` |
| The card holdings / decks | `PlayerModel.Cards`, `GameModel.CardDecks` (`CardListModel`), `CardModel` (stub), `CardJsonImport` |

---

## 1. Purpose & the founding constraint

Cards are the largest and riskiest sub-system, and the one previous attempts
foundered on (`game-engine.md` §11). The decisive tractability constraint stands,
but it is worth restating precisely, because the data-driven shape below looks at
first glance like the thing §11 warned against:

> **Cards are a closed, finite, hand-written set — not a user-authored DSL.**

`game-engine.md` §11 rule 2 offers two ways to honour that: *"a handler class, or a
data row keyed to a known effect-type enum."* This design takes the **second**: a
card is **data** — a list of typed actions over a **closed action vocabulary** (§3).
That is *not* the open-ended effect interpreter §11 rules out, because:

1. **The action vocabulary is closed and finite.** New card behaviour means either
   reusing an existing action type or adding *one more* to a fixed, version-controlled
   list — never authoring free-form effects.
2. **Each action type is hand-written and individually tested, once.** Most are a
   **thin dispatch** to an already-built, already-tested service (`Money` →
   `TransactionService`, `Movement` → `MovementService`, `Jail` → `JailService`,
   `Loans` → `LoanService`, `Building`/`Property` → `PropertyService` /
   `PropertyTransferService`, `Direction` → `PlayerModel.FlipDirection`, `Turns` →
   `ExtraTurns` / `TurnsToMiss`). **Some introduce genuinely new engine code** —
   NOPE, "collect rent while in jail", "jailed for 10 turns and collect rent". That
   is expected and fine: the *set* is closed, so each new action is implemented and
   tested deliberately, not interpreted on the fly.

So the line this design must not cross is **not** "no new code per action" — it is
"no card-specific behaviour that isn't expressed as a known action type." A card is
data; an action type is code.

### Worked examples (the spread)

- **"Swap with any player"** — a `Movement` action. `TargetPlayer` prompt → move
  the holder onto the target's index and the target onto the holder's index, both
  `CounterDirectionOfTravel` so neither collects GO money, and **without** calling
  `BoardService.ResolveBoardSpaceForPlayer` — a swap does not perform the landed
  space's action (`game-rules.md` Movement rule 4). Emits the existing
  `PlayerSwappedReceipt`. Pure reuse of `MovementService`.
- **"Go to jail for 10 turns and collect rent"** — partly seamed
  (`PlayerModel.MaxJailTurnsOverride` already exists and the orchestrator reads it),
  partly new (a held "collect rent while jailed" state that overrides `game-rules.md`
  Default rule 2).
- **"Next time in jail, collect rent"** — a held flag consulted by the rent path,
  again overriding Default rule 2.
- **NOPE / immunity** — wholly new (the counter mechanism, §6).

---

## 2. The shape of a card — Type, Groups, Actions, Conditions

A card carries display text, a **`CardType`** (the existing enum — governs *which
deck* it belongs to and *when it is drawn*, the collection rules of `game-rules.md`
Cards; see §9), and three structured parts:

- **Groups** are the *choosable options* on a card. A group is one-or-more
  **Actions**; actions within a group are **AND**ed (all happen). Multiple groups
  are **OR**ed — they form a choice the player makes. One group ⇒ no choice (apply
  it). Two-plus groups ⇒ a choice, surfaced via the existing **`CardOptionPrompt`**
  (one option per group). Where an action inside the chosen group needs follow-on
  targeting, it composes with `TargetPlayerPrompt` / `TargetPropertyPrompt`
  (`choice-events.md` §15.9).
- **Actions** are *what happens* — the closed vocabulary of §3. At least one per
  group. **Nested inside their group** (not a flat sibling list keyed by `GroupId`):
  these are JSON snapshot POCOs, so nesting serialises fine, reads naturally, and
  makes "≥1 action per group" structurally true rather than an invariant to police.
- **Conditions** are *when* a held card may (or must) be played — the state and
  trigger that make it live (§5).

---

## 3. Actions — the closed vocabulary

The exhaustive set (extend deliberately; each addition is a code change, a test,
and — where it alters a default — a `game-rules.md` "unless a card states
otherwise" reference). The authoritative inventory is the Excel sheet
(`config/Monopoly Cards and Types.xlsx`):

- **Building** — purge, grant a house/hotel, etc.
- **Card** — give a card to a player, steal a card, etc.
- **Direction** — change one player's / all players' direction.
- **Dice** — downgrade a triple, cancel a triple, convert a double to a triple, etc.
- **Immunity** — a held counter to a specific action type (§6).
- **Jail** — Get Out of Jail Free, send a player to jail, collect rent while in
  jail, etc.
- **Loans** — repay all loans, wipe all loans, etc.
- **Money** — any transfer between players / bank / Free Parking.
- **Property** — any title move between players / bank / Free Parking.
- **Turns** — extra turns, miss turns, etc.
- **Movement** — any player movement, including swaps.
- **NOPE** — the universal counter to any action applied to you (§6).

Each is a `CardAction` subclass carrying its own parameters (amount, target count,
direction, etc.). The interpreter is a `switch` over the subtype that calls the
relevant service — thin where it can be, bespoke where it must be (§1).

---

## 4. Two interaction modes

A card touches the engine two ways. The Groups/Actions model captures the first
cleanly; the second is the harder integration and is where Conditions live.

### (a) Active play — *push*
The holder plays a card that *does* something: choose a group, apply its actions.
This is the straightforward path and what every "play card" surface will drive.

### (b) Override-on-draw — *pull* ("unless a card states otherwise")
`game-rules.md` is woven through with *"unless a card states otherwise"* — GO bonus,
Go-To-Jail, Free Parking payout, Tax, the double direction-change. The flow there is
**land → draw the space's card → the card's action supersedes the default, else the
default runs.** That is exactly what every `// TODO take card, do what card says,
fall back to default` in `BoardService` / `GoService` / `TaxService` / `JailService`
/ `FreeParkingService` is stubbed against, and the §11.3 "override pipeline."

The resolution: **a freshly-drawn card resolves immediately, and its action
replaces the default.** A drawn card is therefore `ConditionType.None` — "resolve
now, not kept." The default only runs when no card is drawn, or the drawn card's
action defers to it. This gives `None` a precise meaning (§5) and turns mode (b)
into a special case of mode (a): draw, then apply.

---

## 5. Conditions & triggers

The least-formed part. A condition answers two orthogonal questions:

1. **What event makes the card live?** — the *trigger*. Land on GO, pass GO, another
   player passes GO, a triple rolled, a double rolled, sent to jail, …
2. **How does the player engage it?** — the *type*: forced (play it there and then)
   vs choice, on the cardholder's own turn vs any player's turn.

### Triggers — a `[Flags]` enum the engine raises
`TriggerEvent` is a flags enum: most cards trigger on a single event, some on
several, **always OR**ed ("on land-GO **or** pass-GO"). The key leverage: **the
trigger points are very nearly the same set as the `CiteRule` branch points.** The
engine already stops and names every distinct branch (`Go_LandOn`,
`Go_PassClockwise`, `Roll_DiceNumberByOther`, `Double_*`, `Triple_*`, …). A held-card
evaluation hook at those points asks "does any held card's trigger match here?" — so
this *reuses the branch map the engine already maintains*, not a new event bus.

A `CardCondition` is an abstract class so a trigger can carry parameters where it
needs them (a bare "land on GO" needs none; "land on a property worth over £X"
would) — the `TriggerEvent` flag is the category, the subclass holds any data.

### Type — one per card, conditions ORed
- **`ConditionType` is a single value per card** (the play-mode below). All of a
  card's conditions share it.
- **A card's `CardCondition`s are ORed** — live if *any* matches. (Triggers *within*
  a condition OR too.) **Decided:** everything ORs; if a card ever seems to need an
  AND across conditions, it is split into separate cards instead.

```csharp
public enum ConditionType
{
    None,                 // resolve immediately on draw — not kept (mode (b), §4)
    MetCardholderTurn,    // forced, on the holder's own turn
    MetAnyPlayerTurn,     // forced, on any player's turn
    ChoiceCardholderTurn, // optional, on the holder's own turn
    ChoiceAnyPlayerTurn   // optional, on any player's turn
}
```

`IsKeepUntilNeeded` falls out of this: any card with a non-`None` condition is held
until its trigger fires (or, for the `Choice*` types, until the holder chooses to
play it within the trigger window).

---

## 6. Countering an action — response cards (Immunity & NOPE)

**Immunity is *reactive*, not a silent flag.** When an action would **impact** a
player, that player gets the chance to *play a card to stop it* — exactly like NOPE.
Immunity and NOPE are the same kind of thing: **held cards an impacted player plays
to counter an action.** A NOPE is the *universal* counter; an immunity is a counter
*specific to one action type*.

### The flow (the money-swap example)
1. Player **A** plays "swap your money with the player who rolls the highest die".
   Player **B** rolls highest → B is the action's target.
2. **Before applying to B**, the engine checks **B's** held cards for a relevant
   counter — a NOPE, or a matching immunity ("immunity from money swaps"). If B holds
   one, B is **prompted** with the list of counters they may play.
3. B plays the counter → A's action is cancelled (for B).
4. **Then the chain runs the other way:** if **A** holds a NOPE, A is prompted to
   NOPE B's counter — and so on, alternating, without limit.
5. If B holds nothing, or declines, A's action applies as normal. If a counter
   stands uncancelled, A's action becomes a **no-op**.

### Consequences for the model
- **Only impacted players may counter.** A NOPE (or immunity) can only be played by a
  player the action acts on.
- **Counters ride one response window.** This is `InterruptibleWindowPrompt`
  territory (`choice-events.md` §9), generalised to "the impacted player's playable
  counters" rather than NOPE alone — NOPE piggybacks on whatever that window becomes.
- **The engine has drifted from §9 — realign it first.** A dedicated review of the
  interrupt window is a prerequisite before the counter mechanism is built.
- **UX (deferred, but keep in mind):** any card prompt — even a single-action card —
  carries, for impacted players, a **NOPE button** (and any matching immunity)
  alongside the play/choose-option controls.

> **NOPE is left to last.** The mechanism is documented here so the rest of the
> model accommodates it, but NOPE itself is built after the simpler cards and the
> window realignment.

---

## 7. Global-event cards

A card whose effect is table-wide rather than player-scoped (`IsGlobalEvent`) drives
the existing `GlobalEventService` — `StationRent`, `UtilityRent`, `TaxMultiplier`,
`RealFreeParking`, `JailFull` (`GlobalEvent` enum).

**Single active event is the rule, and it's consistent by construction:** *every
event card is a Double card*, and rolling a double wipes the current event
(`GlobalEventService.ClearCurrentEvent`). So a new event card is always played on the
very roll that clears the previous one — two events can never coexist.
`GameModel.GlobalEventInfo` stays a single slot.

---

## 8. Percentage cards

Percentage Chance / Community Chest realise a fraction of their value by the holder's
buildings — 100% with a double hotel, 50% with any buildings, 10% with none
(`game-rules.md` Cards). That tier is already computed: `GameModel.PlayerPercentCap`
returns 100 / 50 / 10. A percentage `Money` action reads it; no new calc.

---

## 9. Decks & draw

The deck **is an ordered list** — no separate draw-pointer or shuffle bag:

1. **At game creation**, the full card list (per `CardType`) is loaded, **shuffled,
   and stored on `GameModel`** (`CardListModel` / `CardDecks`). *The order of the
   list is the deck.*
2. **Drawing** takes the next card off the **front** of its type's deck. Which type
   is drawn is set by the trigger — `CardType` + the `game-rules.md` collection rules
   (land on GO → GO deck, roll a double → Double deck, dice number → Third deck, etc.).
3. **Keeping** (a keep-until-needed card) **removes** it from the deck and adds it to
   the player's `PlayerModel.Cards`.
4. **Returning** a spent/played card **appends** it to the **end** of its deck. (A
   `ConditionType.None` card that resolves immediately on draw returns the same way
   once resolved.)

**Open:** whether the master list is sourced purely from the JSON import or persisted
to the DB (§11.4). Either way the per-game shuffled order lives on `GameModel` so it
snapshots and replays deterministically (`game-engine.md` §8 rule 6).

---

## 10. The model (v1)

```csharp
public class CardModel
{
    public string CardId { get; set; }       // GUID
    public string CardText { get; set; }      // display
    public CardType CardType { get; set; }    // existing enum — deck + collection (§9)

    public IReadOnlyList<CardGroup> Groups { get; set; }         // choosable options (OR)
    public IReadOnlyList<CardCondition> Conditions { get; set; } // when it may be played (OR)

    public bool IsKeepUntilNeeded { get; set; }   // implied by a non-None condition (§5)
    public ConditionType ConditionType { get; set; }

    public bool IsGlobalEvent { get; set; }       // table-wide (§7)
    public bool IsNope { get; set; }              // routes into the counter window (§6)
}

public class CardGroup
{
    public string GroupId { get; set; }                  // GUID
    public string GroupText { get; set; }                // split of CardText for this option
    public IReadOnlyList<CardAction> Actions { get; set; } // ≥1, ANDed — nested here (§2)
}

public abstract class CardAction
{
    public string ActionId { get; set; }   // GUID (may not be needed)
    // Concrete subclass per action type (§3) carries its own parameters.
}

public abstract class CardCondition
{
    public string ConditionId { get; set; }    // GUID (may not be needed)
    public TriggerEvent Triggers { get; set; }  // [Flags] — event(s) that make it live (§5)
    // Concrete subclass carries any trigger parameters.
}

[Flags]
public enum TriggerEvent
{
    None          = 0,
    OnLandGo      = 1 << 0,
    OnPassGo      = 1 << 1,
    OnOtherPassGo = 1 << 2,
    OnRollDouble  = 1 << 3,
    OnRollTriple  = 1 << 4,
    OnSentToJail  = 1 << 5,
    // … grown from the actual card list / Excel inventory (§11) …
}
```

Changes from the v0 sketch: **`CardType` added**; **Actions nested under
`CardGroup`** (was a flat list with `GroupId`); `CardCondition` gains a `[Flags]
TriggerEvent`; root `Actions` dropped (they live in groups).

---

## 11. Open decisions

1. **Immunity representation (§6).** The held-counter card is a *played* card like
   NOPE — settle how the engine matches an incoming action to the immunities that can
   counter it (an action-type key on both sides?).
2. **Interrupt-window realignment (§6).** The engine has drifted from
   `choice-events.md` §9 — a prerequisite review before the counter mechanism is built.
   *(NOPE itself is the last thing built.)*
3. **The `TriggerEvent` ↔ `RuleCode` relationship (§5).** Keep them **loosely
   parallel** — they will diverge a little. Pin down the real `TriggerEvent` set by
   enumerating the conditions actually used across the card list (the Excel sheet);
   a lockstep test only where they're meant to agree (cf. `rule-citation.md` §10).
4. **Card-list source (§9).** JSON import only, or persisted to the DB? The per-game
   shuffled order lives on `GameModel` either way.
5. **`card-decks.md` + the Excel inventory.** The per-card *contents* (every card:
   deck/type, text, groups → actions, conditions) — authored from
   `config/Monopoly Cards and Types.xlsx`, drives implementation the way
   `game-rules.md` drives the engine.

### Decided (kept for the record)
- **All condition/trigger logic ORs** — a needed AND becomes separate cards (§5).
- **Single global-event slot** — every event card is a Double, and a double clears
  the current event, so two can never coexist (§7).
- **Import last** — settle the live-state model and resolution first, then build the
  `CardJsonImport` shape off it.

---

## 12. Traceability

1. **`game-rules.md`** — Cards (types, collection, keeping/playing, NOPE), Purging,
   Jail, and every *"unless a card states otherwise"* the override path (§4b) serves.
2. **`game-engine.md` §11** — the founding constraint (closed set, data-row-or-handler,
   the override pipeline) this doc realises; §11.5 defers contents to `card-decks.md`.
3. **`choice-events.md`** — `CardOptionPrompt` (§15.9, the group choice),
   `InterruptibleWindowPrompt` (§9, the counter window — drifted, §6), and the
   `TargetPlayer`/`TargetProperty` prompts actions compose with.
4. **`rule-citation.md`** — the `CiteRule` branch map the `TriggerEvent` set mirrors
   (§5), and the lockstep-test pattern for keeping two enums honest (§11.3).
5. **`event-receipts.md`** — `CardTakenReceipt` / `CardPlayedReceipt` /
   `PlayerSwappedReceipt` the card paths must emit; `game-stats.md` §13.9 card stats
   unblock once they do.
6. **`config/Monopoly Cards and Types.xlsx`** — the raw action/condition/card
   inventory the `TriggerEvent` set and `card-decks.md` are derived from.
7. **Code** — `Models/Snapshot/Cards/CardModel.cs` + `CardListModel.cs` (the stubs),
   `Enums/Cards/CardType.cs`, `Models/Imports/CardJsonImport.cs`,
   `Services/SubSystems/GlobalEventService.cs`, `GameModel.PlayerPercentCap`, and the
   `BoardService` / `GoService` / `TaxService` / `JailService` / `FreeParkingService`
   `// TODO take card` stubs.

---

## Implementation status & drift

> This document records the **agreed design**, not the live state of the code.
> Since it was written the implementation has moved on — much of what is
> described here is built, and some details have changed. Any status, "TODO",
> "not yet built", or "nothing built" note above may be out of date.
>
> Where this doc and the code disagree, the **code (and the developer) win**
> (`docs/development/README.md`). Verify specifics against the current code.