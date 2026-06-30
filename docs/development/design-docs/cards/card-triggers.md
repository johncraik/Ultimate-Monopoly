# Card Triggers — Held-Card Evaluation & the Override Pipeline

How held (keep-until-needed) cards become playable: the engine, at every meaningful
moment, asks "does any player hold a card that wants to fire here?" and then prompts
(choice) or forces (acknowledge) the holder, applies the card, and lets the card
**suppress or modify** the default action at that point. This is the missing keystone of
the card sub-system — `cards-design.md` settled the *model* (groups → actions, conditions,
the two interaction modes); this doc settles the *evaluation hook* that drives interaction
mode (a) (active/held play) and the override pipeline of mode (b).

**Status:** built. `CardTriggerService` and the held-card evaluation / override
pipeline described here are implemented, on top of `CardService`
(`DrawCard` / `ResolveCard` / `PlayCard`), the per-action `ICardActionService<T>`
handlers, `CardActionHelper.ResolveTargets`, `CardTrigger` (`[Flags]`),
`CardConditionType`, the prompt framework, and the `CiteRule` branch map. The
card list is finalised (`cards.md`), so the per-card tagging the original
"data blocker" waited on is in place. See the drift note at the foot of this
document.

---

## 1. Purpose & the gap it fills

`cards-design.md` §4 names two ways a card touches the engine:

- **(a) Active / held play (push):** the holder plays a card that does something. Until now
  the only held-play path that worked was the forced jail exit (`JailService` →
  `CardService.PlayCard`). Nothing evaluated held cards *against game events* — a card sitting
  in a hand could never fire on "you landed on tax" or "someone took the Free Parking money".
- **(b) Override-on-draw (pull):** land → draw the space's card → it supersedes the default
  (`DrawCard` returns `SuppressDefault`, wired into every board space). Built and live.

This doc is the missing half of (a): the **`CardTriggerService`**, called at every trigger
point, which evaluates held cards and drives the prompt/force/apply/suppress cycle. It is the
substrate the NOPE/immunity counter window (§15) later sits on, and it is what unlocks the
bulk of the real card inventory (everything keep-until-needed).

---

## 2. The core model

At a **trigger point** in the engine, the service is called with:

1. **Which trigger fired** — a `CardTrigger` flag (the moment: land on GO, tax landed, another
   player took Free Parking, …).
2. **The subject** — the player the event is happening *to* (the lander, the roller, the FP
   taker).
3. **Context** — whatever the cards need to read or modify at this point: an amount (the tax,
   the GO bonus, the FP take), a property index, etc.

The service then, for every held card that is **live** (§4):

- **Forced (`Met*`)** → an `AcknowledgePrompt` ("this happens"), apply it — no opt-out.
- **Choice (`Choice*`)** → a "Play X?" prompt (`CardOptionPrompt` shape); on yes, apply; on no,
  move on.

…and returns a **typed result** (§9) describing what the call site must now suppress or change.

The prompting / forcing is the *easy* half — it reuses the existing prompts and
`CardService.PlayCard`. The architecture is in the other three questions: **who is scanned**
(§4), **how a card reads/modifies context** (§6), and **how granular suppression is reported
back** (§9).

---

## 3. The trigger is the gate

A held card is never "playable whenever". It is playable **only when its trigger has actually
fired at the current engine point** — that is the hard gate. The `CardTrigger` flags sit (very
nearly) on the same branch points the engine already cites via `CiteRule` (`Go_LandOn`,
`Go_PassClockwise`, `Roll_DiceNumberByOther`, `Double_*`, `Triple_*`, …), so the trigger hook
rides the branch map the engine already maintains rather than a new event bus
(`cards-design.md` §5).

A card's `CardCondition`s are **ORed** (live if any matches); the `CardTrigger` flags within a
condition are **ORed** too. So "on land-GO **or** pass-GO" is one condition with two flags; a
card needing an AND across conditions is split into two cards (`cards-design.md` §5, decided).

---

## 4. The two axes — who is scanned, and forced vs choice

Two orthogonal questions decide whether a live card prompts or forces, and **whose hand is even
looked at**.

### 4.1 Turn-scope = is the holder the *subject*?

`CardConditionType`'s turn-scope half is best read as **subject vs bystander**, not literally
"whose turn the clock says it is":

- **CardholderTurn** (`MetCardholderTurn` / `ChoiceCardholderTurn`) — the holder **is the
  subject** of this trigger: the event is happening *to them*. For most triggers that only ever
  happens on the holder's own turn (your roll, your double, you land on tax). The one place it
  diverges from "your turn" is **board-space resolution under third-die movement**: when the
  holder is moved by another player's third die, they are *still the subject being moved*, so a
  card like "advance 5 spaces" stays playable. It is playable there not because it is any-turn,
  but because its trigger fires for whoever is resolving the space.
- **AnyPlayerTurn** (`MetAnyPlayerTurn` / `ChoiceAnyPlayerTurn`) — the holder is a **bystander
  reacting** to an event happening to someone else (or table-wide). "Receive the Free Parking
  money another player would have taken" — *B* is the subject, *A* reacts.

> The "not third-die movement" intuition is true of *roll-based* triggers (your roll, your
> double) — those can only happen on your own turn. It is **not** a property of the
> CardholderTurn category itself; board-space resolution is the exception, and subject-based
> framing captures it cleanly.

### 4.2 Scan scope falls straight out of 4.1

| Turn-scope | Hands scanned |
|---|---|
| `*CardholderTurn` | **the subject's hand only** (holder must be the subject) |
| `*AnyPlayerTurn` | **every active player's hand** (any holder may react) |

So `OnLandFreeParking` (the lander's own FP card) is a *cardholder* trigger, while
`OnOtherTakesFreeParking` (someone else's reactive card) is an *any-player* trigger — even
though both happen "at Free Parking". The service decides the scan set from the trigger's
condition-type, not from the board point.

### 4.3 Engagement = forced vs choice

The other half of `CardConditionType`:

- **`Met*` (forced)** — the card fires automatically when live. The holder is *told*
  (`AcknowledgePrompt`) but cannot decline. Example: "your next tax payment is doubled".
- **`Choice*` (optional)** — the holder is *offered* the play and may decline. Example: "steal
  the Free Parking money".

When a holder has **more than one** live card for the same trigger (or one card with multiple
groups), the choice surfaces through the existing `CardOptionPrompt` (one option per playable
card/group, plus — for `Choice*` — a "don't play" option).

---

## 5. `CardTrigger` — the flag set

The `[Flags]` enum, **derived from the held cards in the finalised list (`cards.md`)** — the
keep-until-needed cards that wait on a board/turn event (resolve-on-draw and "anytime on your own
turn" cards carry no trigger). Each value names a moment the engine reaches; the bracketed service
is where the trigger call goes. The "subject" is the player the event happens *to* (the one passed
in); for the any-player triggers a bystander reacts to that subject.

| Trigger | Subject | Engine point | Held cards (cards.md) |
|---|---|---|---|
| `OnLandGo` | lander | `GoService.LandOnGo` | GO money doubled ×5; UNLUCKY no GO money ×5; pay each player on GO |
| `OnPassGo` *(param: anti-clockwise)* | passer | `GoService.CollectGoMoney` | receive £X passing GO anti-clockwise (valid N) |
| `OnOtherPassGo` | passer (bystander reacts) | `CollectGoMoney` on another player | former prisoner steals others' GO bonus |
| `OnLandFreeParking` | lander | `FreeParkingService.ProcessFreeParking` | no cash this FP visit; receive ALL the FP money |
| `OnOtherTakesFreeParking` | taker (bystander reacts) | the FP money take | receive the FP money another would have taken |
| `OnRollDouble` | roller | orchestrator double branch | convert double→triple; dodgy-judge double→triple in jail |
| `OnRollTriple` | roller | orchestrator triple branch | downgrade triple→double |
| `OnOtherRollsTriple` | roller (bystander reacts) | another player's triple | cancel a player's triple bonus |
| `OnSnakeEyes` | roller | the £500 snake-eyes bonus | pay snake-eyes money to the lowest roller |
| `OnInJail` | the jailed | leave-jail path | get-out-of-jail-free; befriend a guard (free next exit) |
| `OnRentDue` | rent-payer | `PropertyService.PayRent` | your next payment to another player is doubled |
| `OnNextMove` | mover | post-move (`MovementService`) | after your next move, forward 23 / back 17 |
| `OnTaxLanded` | lander | `TaxService.PayTax` | your next tax payment is tripled |

> **Retired** against the finalised list (bit values kept stable so persisted JSON still
> deserialises): `OnEnterJail` ("next time in jail receive rent" — cut), `OnPayPlayer` (folded into
> `OnRentDue`), `OnNextRoll` (the real cards are `OnNextMove`), `OnCompleteSet` ("house per property
> on a set" — cut), and `OnRentDue`'s old "50% of rents on a chosen set" meaning.

---

## 6. Context-aware effects — the amount problem

The two motivating cards are **modifiers**: they don't do a fixed thing, they transform the
value at the trigger point.

- "double tax" needs the **tax amount** (£200 → pay £400).
- "steal FP money" needs the **FP take amount** (receive exactly what the other player would).

There is **no dedicated "modifier" action** in the vocabulary, and there shouldn't be — the
right home is `MoneyAction`, which **already derives its amount from dynamic context** three
ways: `PercentageApplies` (reads `PlayerPercentCap`), `DiceMultiplier` (reads a fresh roll),
`PerUnit` (reads building / property counts). "Read the triggering amount" is a fourth source
in the same family. So:

- Add **`AmountSource { Fixed, TriggerAmount }`** to `MoneyAction`. When `TriggerAmount`, the
  action's base is the context amount the trigger supplied, and `Amount` is reused as the factor
  (`× 2` for "double", `× 1` for "exactly that amount").
- "double tax" = `MoneyAction { AmountSource = TriggerAmount, Amount = 2, Direction = Pay,
  Counterparty = FreeParking }`.
- "steal FP" = `MoneyAction { AmountSource = TriggerAmount, Amount = 1, Direction = Receive,
  Counterparty = FreeParking }`.

This is consistent with the percentage-card precedent (`cards-design.md` §8) — amount derived
from engine context, no new action type. The context is threaded into card resolution: the
action services gain an optional context parameter that only the `TriggerAmount` source reads.

---

## 7. The card does its own work

**Decided:** the card *executes* its own effect through the normal action services, even where
that duplicates logic the engine already has — the "double tax" card pays the £400 itself via a
`MoneyAction`; it does not hand a modified number back for the engine to pay. The trigger result
(§9) only declares **suppression** (so the engine skips its own default), never the payment.

The trade is deliberate: a little duplication for a card model that stays uniform and
self-contained — every card is "a group of actions that run", whether resolve-on-draw, played
from hand, or fired by a trigger. No special "the engine applies the card's modification" path.

---

## 8. `CardTriggerService` — one typed method per trigger

Mirrors `TransactionService` / `PropertyTransferService`: a **private core** (scan the right
hands → resolve live cards → forced-or-choice prompt → `PlayCard`), and **public methods, one
per trigger**, each carrying exactly the context that point has and returning that point's
typed result (§9):

```
OnTaxLanded(engine, lander, taxAmount, ct)                  -> TaxTriggerResult
OnLandGo(engine, lander, goBonus, ct)                       -> GoTriggerResult
OnFreeParkingTake(engine, taker, amount, ct)               -> FreeParkingTriggerResult
OnBoardResolve(engine, mover, ct)                          -> BoardResolveTriggerResult
... one per CardTrigger ...
```

Self-documenting at the call site, and each method knows its own trigger flag, its scan scope
(from the condition-type), and the context it must pass into the card actions. The service is
exposed on the `GameEngine` bundle (like `CardService` / `ShortfallService`) so any sub-service
can reach it without a constructor cycle.

---

## 9. What each trigger returns

Each public method returns a small result telling its call site how to alter (or skip) its
default. The concrete shape is an implementation detail (a typed per-trigger result, deriving from
a common base that carries the shared "did any card fire here?" signal for citations/receipts) —
what matters is **the data the caller needs back**, derived from the held cards in `cards.md`:

| Trigger | Caller | Data back |
|---|---|---|
| `OnLandGo` | `GoService.LandOnGo` | **Skip the £200 GO bonus?** A card *doubled* it (pays ×2 itself) or *cancelled* it (UNLUCKY). The additive "pay each player on GO" card does **not** suppress. |
| `OnPassGo` | `GoService.CollectGoMoney` | **Nothing to skip** — the pass bonus always runs; the anti-clockwise "receive £X" cards pay extra themselves. (Just the "a card fired" signal.) |
| `OnOtherPassGo` | `GoService.CollectGoMoney` (passer) | **Skip the passer's GO bonus?** A bystander's "former prisoner" stole it. |
| `OnLandFreeParking` | `FreeParkingService.ProcessFreeParking` | **Which FP sub-steps to skip** — in practice the **money take** (no-cash-this-visit, or receive-ALL-the-money replacing the capped take). Fine / property-take / hand-in / purge are independently suppressible. |
| `OnOtherTakesFreeParking` | `FreeParkingService` (the money take) | **Skip the taker's FP money take?** A bystander received it instead. |
| `OnRollDouble` | `PlayerTurnOrchestrator` (double branch) | **The effective roll type** — unchanged double, or **upgraded to a triple** (re-route: combined move, triple bonus, no third die). |
| `OnRollTriple` | `PlayerTurnOrchestrator` (triple branch) | **The effective roll type** — unchanged triple, or **downgraded to a double** (re-route: two-dice move, direction flip). |
| `OnOtherRollsTriple` | `PlayerTurnOrchestrator` / triple-bonus payout | **Skip the roller's triple-bonus PAYOUT?** A bystander cancelled it — the +£500 accumulator still increments. |
| `OnSnakeEyes` | snake-eyes £500 payment | **Nothing to skip** — the £500 still pays; the redirect card receives it then pays it onward itself. |
| `OnTaxLanded` | `TaxService.PayTax` | **Skip the default tax payment?** A held "next tax tripled" card paid a modified amount to Free Parking instead. |
| `OnRentDue` | `PropertyService.PayRent` | **Nothing to skip** — default rent runs; "next payment doubled" pays an equal extra to the same owner (which must be threaded **in** as context). |
| `OnNextMove` | `MovementService` (post-move) | **Nothing to skip** — the card performs an extra board move itself, and that new landing resolves normally. |
| `OnInJail` | `JailService` (leave-jail path) | **Did the holder play a release card (now out of jail)?** So the flow skips the fee and proceeds to move. |

Three kinds of answer fall out: **suppress-a-default** (`OnLandGo`, `OnOtherPassGo`,
`OnLandFreeParking`, `OnOtherTakesFreeParking`, `OnOtherRollsTriple`, `OnTaxLanded`, `OnInJail`); **a
changed value** (`OnRollDouble` / `OnRollTriple` → the effective roll type); and **nothing
actionable** (`OnPassGo`, `OnSnakeEyes`, `OnRentDue`, `OnNextMove` — the card does all its own work).
Even the "nothing actionable" ones still carry the shared *did-a-card-fire* signal.

> **Suppress vs additive is a modelling decision** baked into the table — "rent doubled" and the
> "snake-eyes redirect" are modelled *additive* (card pays the extra itself → no suppress), while
> "GO money doubled" is modelled *replace* (card pays the doubled amount → suppress). Flip a card's
> model and its row flips with it.

Free Parking's sub-actions stay independently suppressible (§11); the rest collapse to a single
flag or value. The granularity lives in the result type, not a sprawl of call-site booleans.

---

## 10. The call-site contract

Every trigger point follows the same shape:

1. **Compute the default outcome** (the amount / the action it would take).
2. **Call the trigger method**, passing the subject and the context.
3. **Read the typed result** and **suppress the parts it names**; apply the rest of the default
   normally.

Worked, for double tax:

1. `TaxService` computes tax = £200.
2. `var r = await engine.CardTriggerService.OnTaxLanded(engine, lander, 200, ct);`
3. Inside: it's a `CardholderTurn` trigger → scan the **lander's** hand → finds the forced
   "double tax" card → acknowledge → `PlayCard`. Its `MoneyAction` (`AmountSource = TriggerAmount,
   Amount = 2`) pays `200 × 2 = £400`. The group's suppress metadata (§11) → `r.SuppressPayment =
   true`.
4. `TaxService` sees `SuppressPayment` → **skips its own £200 charge**.

---

## 11. `SuppressDefault` — the rework

`SuppressDefault` is the card-side declaration of "I replace the default here". Two changes are
needed:

1. **Stop applying it at draw time for kept cards (the bug).** `DrawCard` currently returns
   `card.SuppressDefault` on **both** branches — including the keep-until-needed branch, where
   the card is added to hand and *not yet resolved*. A kept card must not suppress the board's
   default the moment it is drawn; suppression belongs to **when it is later triggered**. Fix:
   the keep branch returns `false`; the **trigger result** is the only thing that suppresses for
   held cards. (Resolve-on-draw cards — `ConditionType.None` — keep the existing draw-time
   suppression; that path is correct.)
2. **Make it granular and group-scoped.** A single card-level bool can't say "suppress the FP
   money but not the property sweep", and a multi-group card's *chosen group* is what decides.
   So suppression is **per-group metadata keyed to what it overrides** (which sub-action(s) of
   the trigger point it replaces), and the trigger service maps the played group's metadata onto
   the typed result (§9). The exact metadata shape — a flag set, or a small enum per trigger —
   is pinned during the card-list sweep (§17), because it depends on which real cards suppress
   which sub-actions.

---

## 12. Advance vs Move — the destination card-draw rule

A crucial distinction between the two movement kinds, decided here:

- **Move** (`MovementKind.MoveSpaces` — "move back 3", and cards *worded* "advance 3 spaces"
  that are really relative moves): the player travels and lands, and the landing is a **full
  resolution including drawing the destination space's own card** (the override-on-draw deck
  draw — land on Chance → draw Chance, land on a Tax space → draw the Tax card, …).
- **Advance** (`MovementKind.AdvanceToIndex` / `AdvanceToNearest` — "advance to GO", "advance to
  the nearest station"): the player is **sent** to the space (a teleport). **No destination
  card is drawn** — "advance to GO" does *not* draw a GO card. The landing still applies its
  non-card consequences (the GO bonus, rent, jail entry, …) **and** still fires the
  held-card triggers (§13).

So the only difference between Move and Advance at the destination is the **deck draw**
(override-on-draw): Move draws the space's card, Advance does not. Held-card triggers fire on
both. Concretely this means board-space resolution needs a **"draw the space's card?"** input —
`true` for rolls and Move cards, `false` for Advance cards — rather than drawing unconditionally
as it does today.

> Watch the wording: several real cards say "advance N spaces" but are **relative moves**
> (`MoveSpaces`), so they *do* draw at the destination. Only "advance/go **to** <named place>"
> is an `Advance`. Confirm each against the finalised list (§17).

---

## 13. Recursion / re-entrancy — loops allowed

**Decided:** a triggered card's effect *can* re-enter the trigger pipeline. "All players advance
to GO" advances each player and **fires the GO held-card trigger for each** (so a held "double
GO money" card still reacts). The service does not block re-entry.

The risk — a pathological cycle (card A triggers card B triggers card A …) — is accepted for
now. Whether any such cycle actually exists is a property of the **real card set**, so the
guard question is revisited when the finalised list lands; if a genuine loop appears, the
narrowest fix (a per-resolution visited-set, or a depth cap) is added then rather than
pre-emptively.

---

## 14. Multi-use lifecycle

`CardGroup` already carries `IsChosenGroup`, `TurnsActive`, and `TurnsRemaining` for multi-use
cards ("valid N times", "for N turns"). The trigger service is where that lifecycle is
**consumed**: when a held card fires, the chosen group is marked, and `TurnsRemaining` is
decremented; the card only returns to its deck (via `PlayCard`) once it is spent (the existing
`PlayCard` already checks `TurnsRemaining` before removing the card from hand). So a "for the
next 5 GO landings" card stays in hand, firing on each `OnLandGo` until its counter hits zero.

---

## 15. Relationship to NOPE / immunity

The trigger service **is** the substrate the counter window sits on. "An impacted player may
play a card to stop this" (`cards-design.md` §6) is just another trigger evaluation — the
impacted player is the subject (or a bystander), and their NOPE/immunity is a live card the
service finds and offers. So building the trigger pipeline correctly is what makes NOPE a small
addition (a counter-flavoured trigger that wraps the action it cancels) rather than a separate
machine. NOPE itself stays **last** (`cards-design.md` §6), built after the simple triggers and
the `InterruptibleWindowPrompt` realignment — but the trigger shapes here are kept compatible
with it.

---

## 16. Worked examples

### 16.1 Double tax (CardholderTurn, forced, suppress, context)
Lander holds "your next tax is doubled" (`MetCardholderTurn`, `OnTaxLanded`). Lands on tax →
`TaxService` computes £200 → `OnTaxLanded(engine, lander, 200)` → scan lander → forced →
acknowledge → `PlayCard` pays `£200 × 2 = £400` (context-amount MoneyAction) → result
`SuppressPayment = true` → `TaxService` skips its own £200. The multi-use counter (if "next" =
once) is consumed; the card returns to deck.

### 16.2 Steal Free Parking money (AnyPlayerTurn, choice, granular suppress)
Player B lands on Free Parking and would take £500. **A** holds "receive the FP money another
player takes" (`ChoiceAnyPlayerTurn`, `OnOtherTakesFreeParking`). At the FP **money-take**
sub-step → `OnFreeParkingTake(engine, B, 500)` → scan **everyone** → A is offered "Play X?" →
yes → `PlayCard`: A receives £500 from FP (context-amount MoneyAction) → result
`SuppressMoneyTake = true` (but **not** `SuppressPropertyTake`) → B takes no money but **still**
sweeps any FP properties.

### 16.3 All players advance to GO (re-entrancy + Advance rule)
A played card advances every player to GO. Each is `Advance`d (`AdvanceToIndex 0`): **no GO card
drawn** (§12), GO bonus paid, and `OnLandGo` fired per player (§13) — so each player's held
"double GO money" reacts. Recursion is allowed; the loop is finite (one pass over players).

---

## 17. Open decisions / blockers

1. **Finalised card list (blocker for the data, not the architecture).** The exact `CardTrigger`
   set, each card's `CardConditionType` (incl. the §4.1 `Self/Other` re-read), and each card's
   suppress metadata (§11) are pinned when the reworked list lands. The Excel is stale. Until
   then, build the pipeline against the architecture and a small TEMP set.
2. **Suppress metadata shape (§11).** Flag set vs per-trigger enum — decided alongside the list
   (depends which real cards suppress which sub-actions).
3. **Board-space resolution "draw card?" parameter (§12).** Thread a flag through the landed-space
   resolution so Advance skips the deck draw while Move/rolls keep it.
4. **`AmountSource` on `MoneyAction` (§6).** The one model change the architecture needs up front
   — add it (with the `Amount`-as-factor convention) and the context parameter on the action
   services.
5. **Recursion guard (§13).** None for now; revisit if the real list produces a genuine cycle.
6. **NOPE/immunity (§15).** Last; keep the trigger shapes compatible meanwhile.

---

## 18. Traceability

1. **`cards-design.md`** — the card model (§2/§10), the two interaction modes (§4), conditions &
   triggers (§5), the percentage-amount precedent (§8), and the NOPE/immunity counter window
   (§6) this service underlies.
2. **`cards-actions.md`** — the derived `CardTrigger` set and the `Self/Other` tags that need the
   §4.1 re-read against the finalised list.
3. **`choice-events.md`** — the prompts the service drives: `CardOptionPrompt` (§15.9, the
   play/decline choice), `AcknowledgePrompt` (forced), `TargetPlayer`/`TargetProperty` (follow-on
   targeting), and `InterruptibleWindowPrompt` (§9, the NOPE window this substrate feeds).
4. **`rule-citation.md`** — the `CiteRule` branch map the trigger points mirror (§3/§5).
5. **`game-rules.md`** — every "unless a card states otherwise" the override pipeline (§1b)
   serves (GO bonus, tax, Free Parking, jail, the double direction-change).
6. **`transactions.md`** — `MoneyAction` routes through `TransactionService`; the payer-POV rule
   and the sign convention the context-amount cards inherit.
7. **`event-receipts.md`** — `CardPlayedReceipt` / `CardTakenReceipt` the trigger plays emit;
   `game-stats.md` §13.9 card stats unblock as these fire.
8. **Code** — `Services/Cards/CardService.cs` (`DrawCard` / `ResolveCard` / `PlayCard`), the
   `ICardActionService<T>` handlers + `CardActionHelper`, `Enums/Cards/CardTrigger.cs`,
   `Enums/Cards/CardConditionType.cs`, `Models/Cards/Conditions/CardCondition.cs`,
   `Models/Cards/Actions/MoneyAction.cs` (the `AmountSource` change), `Models/Cards/CardGroup.cs`
   (the suppress metadata + multi-use counters), and the board sub-services
   (`Go`/`Tax`/`FreeParking`/`Jail`/`Board`/`Movement`) where the trigger calls and the
   Advance-vs-Move draw flag land. **To build:** `Services/Cards/CardTriggerService.cs` + the
   `CardTriggerResult` hierarchy.

---

## Implementation status & drift

> This document records the **agreed design**, not the live state of the code.
> Since it was written the implementation has moved on — much of what is
> described here is built, and some details have changed. Any status, "TODO",
> "not yet built", "to build", or "pre-implementation" note above may be out of
> date.
>
> Where this doc and the code disagree, the **code (and the developer) win**
> (`docs/development/README.md`). Verify specifics against the current code.