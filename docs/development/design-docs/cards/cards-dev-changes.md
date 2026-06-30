# Cards — Dev Changes Catalogue

A living catalogue of **what needs building / changing** to support the real card inventory,
flushed out as the finalised card list (`cards.md`) is written deck by deck. Each entry links
the *evidence* (the cards that demand it) to the *model* it touches, so the later
`CardTrigger` / action-vocabulary sweep has a worked list rather than a re-read.

**This is a worklist, not a design.** The architecture lives in `cards-design.md` (model) and
`card-triggers.md` (held-card evaluation). This doc only tracks the *deltas* those docs imply.

## Sources & how to read the evidence links

- **`cards.md`** — the finalised card list, now with John's per-card modelling notes. Cited as
  `cards.md → <Deck> → "<card text>"`.
- **`cards-actions.md`** — the verbatim (stale) Excel extract; rows by `No.`. Cited `#<No.>`.
- **`cards-design.md`** — the model (§3 vocabulary, §4 modes, §5 conditions/triggers, §6
  NOPE/immunity, §7 global events, §8 percentage, §10 v1 model).
- **`card-triggers.md`** — the held-card pipeline + §17 open decisions.
- **Snapshot models** — `MP.GameEngine/Models/Snapshot/` (`GameModel`, `PlayerModel`,
  `EventInfo`, `LoanModel`, …). Most card primitives already live here.

**Built today:** only **Money / Movement / Jail** have an `ICardActionService<T>`
(`MP.GameEngine/Services/Cards/Actions/`). Everything else is unbuilt as a *card action* — but,
as the models below show, the underlying **state already exists** in most cases, so the work is
mostly thin wrappers + the `CardService` refactor (§3.8), not new engine mechanics.

**Status legend** — ⬜ not started · 🟡 partial / primitive exists · ✅ done · ❓ needs confirmation.

---

## 1. Action-category services (the `cards-design.md` §3 vocabulary)

Built so far (2026-06-15): Money/Movement/Jail (original) + Turns/Direction/Loans/Building/Property +
**GlobalEvent** (§3a) + **DeckDraw** (Card draw-from-deck) — all resolve-on-draw scope. The rest each
need a `CardAction` subclass + `ICardActionService<T>` + a `switch` arm + a DI line. "Engine
primitive" = the state/mutation already exists.

| Category | Service | Engine primitive (snapshot model) | Evidence |
|---|---|---|---|
| **Money** | ✅ built (+ §2 sub-modes) | ✅ `TransactionService` | — |
| **Movement** | ✅ built (+ §2 modes) | ✅ `MovementService` | — |
| **Jail** | ✅ built (+ §2 flags) | ✅ `JailService`; `PlayerModel.JailCost` / `JailTurnCounter` / `MaxJailTurnsOverride` | — |
| **Property** | 🟡 `PropertyActionService` (return/hand-in incl. **set**, take-from-bank, receive-all-FP, clear-FP) | 🟡 `PropertyTransferService`; **swap** (single/set + purge) still to build | `Third → "Return a property…"`, `"Hand in any property…"` · `Triple → "Choose any available property from the bank"`, `"return a set to the bank"`, `"Swap a set…"` |
| **Building** | ✅ `BuildingActionService` (purge self/chosen + grant-hotel) | ✅ `PurgingService` + `RentLevel` bump | `Third → "Purge 2 of your properties"`, `"Purge an opponent's property…"` · `Tax → "receive a free hotel (if there are hotels available)"` |
| **Loans** | ✅ `LoansActionService` (wipe-all, repay-all) | ✅ `PlayerModel.Loans` + `TransactionService.RepayLoan` | `Third → "All outstanding loans are wiped out…"` · `Triple → "Repay all your loans in full"` |
| **Direction** | ✅ `DirectionActionService` (flip self/all) | ✅ `PlayerModel.FlipDirection()` | `Third → "Change direction…"`, `"Each player changes direction, including any in jail"` · `Double → "…do not turn around"` (§2.7) |
| **Dice** | ⬜ | 🟡 `PlayerModel.TripleBonus` (the named amount cards read), `DoublesInRow`/`TriplesInRow`; mid-turn double↔triple conversion seam to confirm | `Third → "Cancel a players triple bonus"`, `"Convert a double into a triple"`, `"…downgraded to a double"` · `Triple → triple-bonus cards (§2.13)` · `Double → "Your double is converted into a triple"` |
| **Turns** | ✅ `TurnsActionService` (miss/extra) | ✅ `PlayerModel.TurnsToMiss` / `ExtraTurns` | `Third → "Have an extra 3 turns"`, `"Miss 3 turns"`, `"…or make the player rolling the lowest… miss 3 turns"` |
| **Card** | 🟡 `DeckDrawActionService` (draw-from-deck) | 🟡 `PlayerModel.Cards`; **pass** (to dice-off-lowest) + **steal** (chosen) still to build | draw-from-deck: `Chance/ComChest → "…or take a Chance"` · pass/steal: `FP → "Pass any retained card… to the player rolling the lowest…"`, `"Steal any card from any player"` |
| **Immunity** | ⬜ (deferred — NOPE substrate, `cards-design.md` §6) — but **now required by the real decks**, multiple types keyed to the action countered | n/a | `GO → "Immunity from swapping all money…"` · `JV → "Immunity from any card drawn when landing on Go To Jail…"` · `FP → "Immunity from triple bonus being cancelled, or a triple being downgraded"`, `"…from one property being purged"` · `GoToJail → "Immunity from returning a property to the bank"` |
| **NOPE** | ⬜ (deferred — last) | n/a | `cards-actions.md` §"Reactive counters" |

> The new decks did **not** force a 13th category. Global-event cards (§3a) reuse the existing
> `EventInfo`; draw-from-deck (Card) recurses the draw pipeline.

---

## 2. Flags & sub-modes on existing actions

| # | Flag / sub-mode | On | Status | Evidence / model |
|---|---|---|---|---|
| 2.1 | **10-turn locked jail** — roll but can't leave + collect rent | Jail | 🟡 `MaxJailTurnsOverride` exists; the "can't leave" + "collect-rent-while-locked" flags to add | `Third → "Go to jail for 10 turns…"` |
| 2.2 | **Hand-in a property to Free Parking** — Property title→FP (*not* purge), "not recorded", no-op if none | Property | 🟡 `FPHandedInSets` is the recorded-history list; "not recorded" = don't append to it | `Third → "Hand in any property into free parking…"` |
| 2.3 | **"Including those in jail"** — each-player direction flip not excluding jailed | Direction | ⬜ | `Third → "Each player changes direction, including any in jail"` |
| 2.4 | **`CollectGoBonus` / "do not pass GO"** | Movement | ✅ done (2026-06-12) | — |
| 2.5 | **Leave-jail-fee modifier** — set (£50) or multiply (×3) | Jail | 🟡 on `PlayerModel.JailCost` | `Triple → "…reset to £50"` · `Third → "…tripled"` |
| 2.6 | **Nearest-of-kind movement + ownership filter** — station / station-owned-by-other / buildable-owned-by-other / coloured-owned-by-other | Movement | ⬜ | `ComChest → "nearest station"` · `%ComChest → "nearest station owned by someone else"` · `Double → "nearest buildable property owned by another player"` |
| 2.7 | **"Do not turn around"** — suppress the default direction-change here | Direction | ⬜ | `Double → "Collect £500 and do not turn around"` |
| 2.8 | **FP money suppress / receive-all** on next visit (held) | Money | ⬜ | `Third → "no cash on your next visit to free parking"` · `Double → "…receive all the money"` |
| 2.9 | **Swap-all-cash** with a player (chosen or dice-off) | Money | ⬜ | `Third → "Swap all your money with another player"` |
| 2.10 | **Per-property charge** — £N × properties owned | Money | ⬜ | `Triple → "£250 times the number of properties you own"` · `%ComChest → "£200 * …"` |
| 2.11 | **Grant a hotel "if available"** (no-op if none) | Building | ✅ `BuildingAction{GrantHotel}` | `Tax → "receive a free hotel (if there are hotels available)"` |
| 2.12 | **Set-level property ops** — return a **set** ✅ (`PropertyAction.Set`); swap-a-set + set-purge ⬜ | Property/Building | 🟡 | `Triple → "return a set to the bank"`, `"Swap a set… Both sets get purged"` |
| 2.13 | **Roll-bonus as a named amount** cards read/redirect/scale/suppress — `TripleBonus` (×die, ×2, give via dice-off, suppress) and the **snake-eyes** (double-1) £500 bonus redirect | Dice/Money | 🟡 `PlayerModel.TripleBonus` exists; snake-eyes bonus is a double-roll effect (see §6) | `Triple → "…receives your triple bonus"`, `"Multiply your triple bonus…"`, `"…doubled"`, `"do not receive…"` · `Third → "Pay the money you receive for snake eyes to the player who rolls the lowest"` |
| 2.14 | **Compound dice multiplier** — two-dice total × third die (richer than the single-die `DiceMultiplier`) | Money | ⬜ | `GO → "Roll 2 dice. Multiple that value by the third die… multiplied by £200"` |
| 2.15 | **Leave-fee set-to-zero / swap** — extends §2.5: next leave is free (`JailCost = 0`), and **swap `JailCost`** between two players | Jail | 🟡 on `PlayerModel.JailCost` | `JV → "befriend a prison guard… cost you nothing"`, `"Swap places with any other player in jail. Your jail fees… also swapped"` |
| 2.16 | **Jail-term modifier** — double the existing term (cap 6) + **lock for N turns** (roll but can't leave) | Jail | 🟡 `MaxJailTurnsOverride` / `JailTurnCounter`; the lock-count flag to add | `GoToJail → "…jail term is doubled (maximum of 6 turns) and must remain in jail for 3 turns… cannot leave"` |
| 2.17 | **Forced (involuntary) property → FP** — "a corrupt judge steals one of *your* properties into FP", not the holder's free choice | Property | 🟡 `PropertyTransferService` + `FPHandedInSets` | `GoToJail → "Corrupt judge steals one of your properties and puts it into free parking…"` |
| 2.18 | **Free-Parking pot as a money/property source-sink** — receive a % of `FreeParkingAmount`; receive **all FP-held properties**; **clear the FP pot** (money + property) to the bank; take-from-FP with a **bank shortfall backstop** | Money/Property | 🟡 `GameModel.FreeParkingAmount` exists; property-pot + clear-to-bank to build | `FP → "All players receive… 50% of the amount in free parking"`, `"You will receive all properties in free parking"`, `"All money and properties in free parking are returned to the bank"`, `"take £3000… the bank pays any shortfall"` |
| 2.19 | **Swap board positions between two players** — chosen, a jailed player, or **board-relative** (the "nearest player ahead" selector defined in §4) | Movement | ⬜ | `GO → "Swap spaces with any other player…"` · `JV → "Swap places with any other player in jail…"` · `FP → "Swap places with the player in front of you travelling in the same direction…"` |
| 2.20 | **Each-player movement to Just Visiting** (excluding jailed + self) — "call a meeting" | Movement | ⬜ | `JV → "You call a meeting. All other players not in jail advance to Just Visiting"` |

---

## 3. Framework / model changes (cross-cutting)

| # | Change | Status | Reference |
|---|---|---|---|
| 3.1 | **`CardTriggerService` + `CardTriggerResult` hierarchy** | ⬜ designed | `card-triggers.md` §8/§9 |
| 3.2 | **`AmountSource { Fixed, TriggerAmount }` on `MoneyAction`** — the spine of the whole **Tax** deck (×3/×2/×½/receive/refund×die) and the held tax/payment/GO modifiers. **Must work in the override-on-draw path** (a drawn Tax card reads the landed tax amount), not only for held cards. Also needs a **fractional factor** (×0.5 for "pay half" / "halved") | ⬜ | `card-triggers.md` §6, §17.4 · `Tax →` all |
| 3.9 | **`CardTrigger` enum missing a Tax trigger** — `cards.md → Third → "Your next tax payment is tripled"` is a *held* tax modifier that needs to fire on a future tax landing, but the enum has no `OnTaxLanded`/`OnTaxDue` flag (it ends at `OnCompleteSet`). Add it | ⬜ | `Enums/Cards/CardTrigger.cs` · `Third → "Your next tax payment is tripled"` |
| 3.10 | **Amount = a fraction of the holder's own cash** — "hand back half of your money" is 50% of `PlayerModel.Money`, a new amount basis distinct from `PercentageApplies` (the %cap) | ⬜ | `Triple → "Hand back half of your money…"` |
| 3.3 | **Granular, group-scoped suppress metadata** on `CardGroup` | ⬜ | `card-triggers.md` §11 |
| 3.4 | **`SuppressDefault` draw-time bug** (kept cards must not suppress at draw) | ⬜ | `card-triggers.md` §11.1 |
| 3.5 | **"Draw the space's card?" flag** through board resolution (Advance skips, Move/rolls keep) | ⬜ | `card-triggers.md` §12, §17.3 |
| 3.6 | **Effect-lifetime / duration model** — "for the next 5 occasions", "next time", "valid N times", **and the global "until another double is rolled"** (§3a) | ⬜ | `cards-actions.md` §"Two adjacent axes" |
| 3.7 | **Target-selection modes** — **(a) dice-off** (`DiceService.RollCardDice`, highest/lowest roller, ~9/10 of targeted cards) as the picker for *any* consequence (pay tax, miss turns, swap, receive triple bonus); **(b) `ChosenPlayer`** ("any player you want"). No *seat*-positional targeting. **(c) a confirmed *board*-relative selector** — "nearest player ahead" (defined in §4), used by the FP swap (§2.19). Dice-off built for money; generalise its target-use | 🟡 dice-off built (money only) | §4 · `FP → "…the player in front of you…"` |
| 3.8 | **`CardService` abstract in / abstract out** *(John's steer)* — thread context into **DrawCard** (and play/trigger) via an **abstract input-data** class, and return an **abstract response** whose concrete type depends on the caller / card type. Generalises `card-triggers.md` §6 (context-in) + §9 (`CardTriggerResult`, response-out) so draw / play / trigger share one signature shape (e.g. a Tax draw passes the tax amount in, gets a suppress-payment response out) | ⬜ to design | `card-triggers.md` §6/§9; `CardService` |

### 3a. Global-event cards — ✅ **built** (`GlobalEventActionService`)

The **Double** deck's 5 global-event cards map **1:1 onto `GameModel.GlobalEventInfo`
(`EventInfo`)** + the `GlobalEvent` enum, which already exist with their read-hooks (the
"no-op when 0 / multiplier" behaviour lives in rent/tax/FP/jail). So the store and the *reading*
are done; the gaps are only **(1) a card action that sets the effect** and **(2) the
"until another double is rolled" clear-condition** (the §3.6 duration model).

| Card (`Double →`) | `EventInfo` field |
|---|---|
| Energy Crisis — utility rent ×10 | `UtilityRentMultiplier = 10` |
| Rail Strike — no station rent | `StationRentMultiplier = 0` |
| Tax Rise — taxes doubled | `TaxMultiplier = 2` |
| FREE Free Parking — FP disabled | `RealFreeParking = true` |
| Prisons at max capacity — pay fee instead of jail | `JailFull = true` (its code comment already quotes this card) |

> **Done.** Store + read-hooks + clear-on-double were already built; the set-from-card seam is now
> `GlobalEventActionService` (`GlobalEventAction{Event, Multiplier?}` → `GlobalEventService.Start*Event`).
> All 5 Double global-event cards are buildable.

### 3b. Override-on-draw suppress metadata — **now specified per card** (unblocks §3.3/§3.4/§3.5)

`card-triggers.md` §11/§17.2 flagged the granular suppress metadata as *blocked on the card
list*. The four draw-decks now carry it explicitly: **every** GO / Free Parking / Go-To-Jail card
(and the Tax deck) is annotated **"Default … occurs as normal"** vs **"Default … is suppressed"**,
and several name *which* sub-action is suppressed (e.g. `FP → "no fine/take/hand in/purge"`). That
is the per-group, sub-action-scoped suppress data §3.3 needs — the blocker is cleared for the draw
decks; the metadata shape can now be pinned against real data rather than guessed.

One subtlety to model (`GO → "UNLUCKY! No money for landing on GO… Default GO action occurs as
normal (but is cancelled due to GO card; which modifies player)"`): suppression isn't always at
draw — a held effect can cancel the default on a *later* landing. That's the §3.4 distinction
(draw-time vs trigger-time suppression), now with a concrete card.

---

## 4. Modelling decisions captured (so they're not re-litigated)

- **if/else = multiple groups + `CardOptionPrompt`** (`Tax → "Pay triple tax or pay half…"` etc.).
- **"Advance up to N spaces" = N movement groups** via `CardOptionPrompt` — no quantity prompt.
- **Deterministic compound actions ≠ branches** — snapshot state then act (the loans-wipe card).
- **"Go to jail" = a Movement advance to the jail index (100), not a dedicated jail action**
  (John's note). Only **sending *another* player** to jail needs a targeted Jail action.
- **Self-targeted "go to jail" cards are intentional** (the 10-turn hide-and-collect, §2.1).
- **Hand-in ≠ purge** — hand-in is Property→FP (`FPHandedInSets`); purge is `PurgingService` /
  `IsPurged`/`HasBeenPurged` / `PropertyPurgedReceipt`. Purge **is** now in the Third deck.
- **Percentage cap follows the *affected* player** (receiving/paying), not always the cardholder —
  a per-action flag (`%ComChest` cards specify it both ways).
- **Targeting is dice-off or ChosenPlayer only** (§3.7) — no positional targeting exists.
- **Jail movement vs Jail action** *(heuristic)*: genuine **movement in/out of jail** (e.g. mass
  breakout → Just Visiting) → a **Movement** action; jail cards that do more than move a player →
  a dedicated **Jail** action. Decided case by case.
- **"Nearest player ahead" target selector** (the §2.19 / §3.7c board-relative mode): from the
  mover at board index *i* travelling direction *D*, take the players **ahead** of *i* in *D*;
  **prefer the nearest one also travelling *D*; else the nearest ahead in any direction.**
  *Worked:* A@10 clockwise, B@12 anti-clockwise, C@34 clockwise → A picks **C** (same direction,
  first ahead); B is nearer but opposite-direction, so it's the fallback only. Board-position, not
  seat-position.
- **Dice conversion/cancel applies *before* the row counters update** — a triple downgraded to a
  double must **not** increment `TriplesInRow` and **must** increment `DoublesInRow` (and vice
  versa). So `Dice`-conversion cards resolve at the `DrawCard(CardType.Double/Triple)` seams
  (`PlayerTurnOrchestrator` TODOs, the `Double`/`Triple` cases) *before* the effective roll-type
  is committed to the counters.

---

## 5. Per-deck review log

| Deck | In `cards.md` | Reviewed | Notes |
|---|---|---|---|
| Chance | ✅ 16 (+notes) | ✅ | Resolve-on-draw Money/Movement/Jail; "go to jail = move to 100"; per-unit repairs. |
| Community Chest | ✅ 16 (+notes) | ✅ | + draw-from-deck ("take a Chance"), each-player collect, nearest-station. |
| % Chance | ✅ 16 (+notes) | ✅ | Percentage + DiceMultiplier + dice-off (highest) — all built. |
| % Community Chest | ✅ 16 (+notes) | ✅ | + cap-per-affected-player (§4), swap-places-chosen, mass-breakout (§6). |
| **Third** | ✅ 35 | ✅ | Property/Loans/Direction/Dice/Turns/Building(purge); §2.1/2.2/2.3/2.5/2.8/2.9; AmountSource modifiers. |
| **Double** | ✅ 10 | ✅ | **§3a global events ×5 (→ `EventInfo`)**, "do not turn around" (§2.7), nearest-buildable-owned, double→triple. |
| **Triple** | ✅ 10 | ✅ | Triple-bonus manipulation (§2.13), set-level Property (§2.12), Loans repay-all, leave-fee reset (§2.5), per-property charge (§2.10). |
| **Tax** | ✅ 10 | ✅ | The **AmountSource** deck (§3.2) via override-on-draw; tax-payer redirect by dice-off (§3.7); "every player pays"; grant-hotel (§2.11). |
| **GO** | ✅ 10 | ✅ | Override-on-draw + suppress annotations (§3b); compound dice-mult (§2.14); held GO-bonus modifiers/duration; **Immunity** appears; cap-at-cardholder. |
| **Just Visiting** | ✅ 10 | ✅ | `OnNextMove` held (+23/−17, "roll or third die"); leave-fee set-0/swap (§2.15); call-a-meeting (§2.20); steal-GO-bonus; **Immunity**. |
| **Free Parking** | ✅ 10 | ✅ | FP pot as money/property source-sink (§2.18); **Card** pass/steal; board-relative swap (§2.19/§3.7); suppress annotations (§3b); **Immunity** ×2. |
| **Go To Jail** | ✅ 10 | ✅ | Jail-term modifier (§2.16); forced property→FP (§2.17); conditional held double→triple "only in jail"; multiple suppress-jail / move-to-JV (§3b); **Immunity**. |

---

## 6. Open questions

No open *design* questions — the card list is complete and every mechanic maps to §1–§4. What
remains is **build work** (the unbuilt action services §1, the flags §2, the framework changes §3)
and one deliberately-deferred area:

- **Immunity / NOPE is in scope, built last** — five+ immunity types across GO/JV/FP/GoToJail key
  to the action countered (money-swap, go-to-jail card, triple-cancel/downgrade, property-purge,
  property-return). Stays **last**, on the NOPE substrate (`cards-design.md` §6 / `card-triggers.md`
  §15); the real decks confirm it's required, not optional.

### Resolved (kept for the record)
- **Global-event clear-on-double is built** — `GlobalEventService.ClearCurrentEvent` in
  `PlayerTurnOrchestrator` (§3a). Only the card-action that *sets* an effect remains.
- **Mass breakout = a Movement action** — every jailed player (index 100) advances to Just
  Visiting (index 10) in the counter direction of travel. (Confirms the §4 jail heuristic.)
- **Dice conversion ordering** — convert/cancel applies *before* the `Doubles/TriplesInRow`
  counters update (§4); some are state-conditional ("only when in jail").
- **"Nearest player ahead" selector defined** (§4) — the board-relative §3.7c mode.
- **`OnNextMove` confirmed** — `JV` "after your next move (roll or third die)…" cards (+23 / −17)
  pin the trigger and that it fires on third-die movement too (`card-triggers.md` §5).
- **Override-on-draw suppress metadata is specified** (§3b) — the draw-deck blocker is cleared.
- **Snake eyes** = a double 1 paying **£500** (`DoubleEffects.SnakeEyesBonus` →
  `TransactionService.ReceiveSnakeEyes`); the Third card redirects that bonus (§2.13).
- **Loans / Dice / Turns primitives exist** — `PlayerModel.Loans`/`LoanModel`, `TripleBonus`,
  `TurnsToMiss`/`ExtraTurns`; **global-event store** = `EventInfo` / `GlobalEvent` (§1, §3a).

---

## Implementation status & drift

> This is a **living worklist** — its ⬜ / 🟡 / ✅ markers track build deltas and
> were accurate when written. The implementation has since moved on, so some
> items still marked outstanding may now be done; verify against the current
> code (and `cards.md`). Where this doc and the code disagree, the **code (and
> the developer) win** (`docs/development/README.md`).