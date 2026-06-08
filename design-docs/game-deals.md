# Deals — Player-to-Player Trades

How two players exchange money and property. A deal is a single, all-or-nothing
swap: one side offers cash and/or properties, the other accepts or declines as a
whole. There is **no counter-offer simulation** — to renegotiate, you start a new
deal (or talk it out around the table). This is the *helper, not simulator*
principle (`game-engine.md` §1) applied to trading: the app records and enforces
the agreed exchange; the haggling is a human conversation.

**Status:** partial / design. Built: the engine primitives this rides on —
`PropertyTransferService.Transfer`, `TransactionService.ProcessDealPayment`
(`FinancialReason.Deal`), `GameModel.CheckReservationRuleSetObtained`,
`PropertyService.NormaliseRentLevels`, the `TurnStateProvider.CanDeal` gate, and
the `ShortfallOutcome.DebtSettled` contract. Stubbed: `DealService`
(`DealForShortfall` returns `true` and applies nothing). Wired: the
`ShortfallService` `ProposeDeal` branch hands off to `DealService` and maps an
accepted deal to `DebtSettled`. Not built: the `DealService` core (`RunDeal` /
apply), the `BuildDealPrompt` / `DealPrompt` pair (+ responses, validator
branches, discriminators), the turn-boundary `ProposeDeal` command
(payload / hub / enqueue), the `DealableProperties` eligibility filter, the
`Deal_*` rule codes, and the front-end deal-builder partial.

---

## 1. Purpose & Scope

A deal moves money and/or property between two players by mutual agreement.

**In scope:**
- The deal contents (money both ways, properties both ways) and what's eligible.
- The two ways a deal is initiated (a turn-boundary command, and a shortfall
  settlement) and the one shared apply path.
- The pause-and-respond flow: the proposer builds, the counter party
  accepts/declines.
- The debt-cancellation semantics when a deal settles a shortfall.

**Out of scope:**
- **Counter-offers.** The app never models a back-and-forth. A declined deal is
  finished; the table negotiates verbally and a new deal is proposed from
  scratch (§9).
- **Policing bad deals.** A lopsided or "cursed" deal that a player agrees to is
  applied verbatim — vetting fairness is not the app's job (§10).
- **The deal-builder UI layout** — a front-end concern (§14); the engine only
  defines the contents and the eligibility.

---

## 2. The Two Entry Points & the Shared Core

A deal is initiated two ways, and they converge on one apply path:

| Entry point | Trigger | Counter party | On accept | On decline |
|---|---|---|---|---|
| **Turn-boundary deal** | Player command at a turn boundary (`CanDeal`) | Freely chosen | Apply the exchange | Nothing (optionally tell the proposer) |
| **Shortfall settlement** | `ShortfallPrompt` → `ProposeDeal` action | **Fixed to the creditor** (`OwedToPlayerId`) | Apply the exchange **and discharge the debt** → `DebtSettled` | Re-prompt the shortfall |

The shared core is one method on `DealService`:

> **`RunDeal(engine, proposer, counterParty, contents, ct) → bool accepted`** —
> opens a `DealPrompt` to the counter party with the contents, parks on it, and
> on **accept** applies the whole exchange (§8) and returns `true`; on **decline**
> returns `false` and applies nothing.

The two callers differ only in (a) how they gather `contents` and (b) what they
do with the `bool`:

- **Turn-boundary command** gathers contents from the command payload (the
  player built them on the Deal tab) and ignores the result.
- **Shortfall settlement** gathers contents from a `BuildDealPrompt` (§7),
  fixes the counter party to the creditor, and maps `accepted → DebtSettled`,
  `declined → loop` (re-prompt the shortfall).

---

## 3. Commands vs Prompts — Where Each Step Lives

The framework split (`choice-events.md` §2, `turn-state.md` §2) carries through
cleanly. Only the **counter party's accept/decline** is a true engine-initiated
prompt. The proposer always builds the deal and submits it whole.

| Step | Direction | Mechanism | Notes |
|---|---|---|---|
| Build the deal (turn-boundary) | Player → engine, engine idle | **Command** (`ProposeDeal`, carries `DealContents`) | The Deal tab *is* the builder; gated by `CanDeal` |
| Build the deal (shortfall) | Engine → proposer, mid-execution | **Prompt** (`BuildDealPrompt`, response carries `DealContents`) | The proposer is reached mid-shortfall, so the builder is emitted as a prompt back to them |
| Accept / decline | Engine → counter party, mid-execution | **Prompt** (`DealPrompt`, response is a bool) | Server-authored contents; the response is just `Accept` |

Why the build step is a *command* on one path and a *prompt* on the other: at a
turn boundary the engine is idle, so the player initiates (command); in the
shortfall branch the engine is already mid-execution resolving a payment, so it
must *ask* the proposer to build (prompt). Either way, the contents flow into the
same `RunDeal`.

---

## 4. What a Deal Is — `DealContents`

A deal is symmetric: each side may put up money and/or properties.

```
DealContents
  MoneyFromProposer        : uint              // cash the proposer gives
  MoneyFromCounterParty    : uint              // cash the counter party gives
  PropertiesFromProposer   : List<ushort>      // board indexes the proposer gives
  PropertiesFromCounterParty : List<ushort>    // board indexes the counter party gives
```

1. **Money is two amounts, netted at apply.** The builder has a money input in
   *each* side's section, so a deal can have cash flowing both ways. The engine
   nets them into a single signed move (§8) — there is no reason to run two
   opposing payments.
2. **Properties are referenced by board index.** Names/colours are resolved for
   display; the engine only needs the indexes.
3. **One-sided deals are valid.** A pure gift (give a property, receive nothing)
   or a pure purchase (give money, receive a property) is a legal deal — the
   builder simply leaves one side empty.
4. **Contents are server-authored on the `DealPrompt`.** The counter party never
   re-supplies them; their response is `Accept: bool`. So the contents are
   trusted once built and validated, and cannot be tampered with on accept.

---

## 5. Property Eligibility — `DealableProperties`

A property is **dealable** when it can leave a player's hands without breaking the
even-building rule or the reservation mechanic:

| Property state / kind | Dealable? | Why |
|---|---|---|
| Owned, no buildings anywhere in its set | **Yes** | A clean transfer |
| **Mortgaged** | **Yes** | Trades fine; the receiver inherits the mortgaged state and its GO liability (`PropertyModel.OwnProperty` preserves it) |
| Built on, **or in a set where any property is built on** | **No** | Trading it would break even-building / the house inventory — sell down to `SET` first |
| **Reserved** | **No** | A reserved property is inert (`game-rules.md` Reserved Properties); it stays put |

**This is *not* `GameModel.TradableProperties` as written.** That method excludes
mortgaged properties (`includeMortgaged: false`) because it was authored for Free
Parking hand-in eligibility — but deals **include** mortgaged. The deal grid
needs a variant (an `includeMortgaged` parameter on `TradableProperties`, or a
dedicated `DealableProperties`) that **includes mortgaged, excludes reserved, and
excludes built-on / in-a-built-on-set**. Reusing `TradableProperties` verbatim
would silently hide mortgaged properties from the grid.

Eligibility is evaluated per side: each entry in `PropertiesFromProposer` must be
dealable for the proposer, each in `PropertiesFromCounterParty` for the counter
party.

---

## 6. Flow — Turn-Boundary Deal (command)

1. A player opens the **Deal tab** on a player profile, picks a counter party,
   and builds the deal (money + property grid, §14). Gated by `CanDeal`
   (`turn-state.md` §3): at a turn boundary (Start or End), engine idle,
   host-bypass aware.
2. **Propose** submits the `ProposeDeal` command carrying `(counterPartyId,
   DealContents)`. The hub pre-checks `CanDeal` and enqueues on the game's
   single-writer executor.
3. On the pump, the work item re-checks `CanDeal` (authoritative, `web-
   orchestration.md` §5), validates the contents (§5, §10), then calls
   `DealService.RunDeal`.
4. `RunDeal` opens a `DealPrompt` to the counter party and **parks the pump**
   (the whole game waits — §10).
5. Counter party (or the host on their behalf) submits Accept or Decline.
   - **Accept** → apply the exchange (§8); the work item completes.
   - **Decline** → nothing applied; optionally an `AcknowledgePrompt` to the
     proposer ("your deal was declined"), then complete.

---

## 7. Flow — Shortfall Settlement (deal that discharges a debt)

This is the path that lets a player settle a debt they can't pay by trading the
creditor something instead. A debt owed to another player (rent or fine) may be
discharged by a direct deal with that creditor — the deal *is* the settlement
(`game-rules.md` Default rule 7; `transactions.md` §6).

1. `TransactionService.Move` charges a debt the player can't afford →
   `ShortfallService.ResolveShortfall` opens a `ShortfallPrompt`.
2. The player chooses **`ProposeDeal`** (only offered when `OwedToPlayerId` is
   non-null — the validator rejects deal-to-bank).
3. `ShortfallService` resolves the creditor and calls
   `DealService.DealForShortfall(engine, debtor, creditor, ct)`, which:
   - Opens a **`BuildDealPrompt`** back to the debtor, with the counter party
     **fixed to the creditor**, to construct the deal.
   - Takes the resulting `DealContents` and calls `RunDeal(debtor, creditor,
     contents)` → opens the `DealPrompt` to the creditor.
4. Outcome:
   - **Creditor accepts** → apply the exchange (§8); `DealForShortfall` returns
     `true`; `ShortfallService` returns **`DebtSettled`** so the outer `Move`
     does **not** apply the original debt. *Example:* B owes C £1000, has £800,
     deals "£500 + Mayfair"; C accepts → C is +£500 +Mayfair, B is −£500
     −Mayfair, and the original **£1000 rent is never charged**.
   - **Creditor declines** → `DealForShortfall` returns `false`;
     `ShortfallService` loops and re-opens the `ShortfallPrompt` so the debtor
     picks another route (loan / mortgage / sell / another deal / bankruptcy).

> **Outcome mapping (the bug to avoid).** An accepted creditor-deal is
> `DebtSettled`, **not** `Bankrupted`. Both currently stop the outer
> transaction, so the difference is invisible today — but once
> `BankruptcyService` lands, returning `Bankrupted` here would wrongly bankrupt
> a player who *successfully settled*. The settling path emits its own deal
> receipts; the original rent receipt must not be emitted.

---

## 8. Apply Semantics — what `RunDeal` does on accept

On accept, `DealService` applies the exchange as a single logical action, in a
deterministic order, reusing the existing money/title seams:

1. **Properties the proposer gives** → `PropertyTransferService.Transfer(engine,
   proposer, counterParty, property)` for each. (`Transfer` is receiver-driven
   and preserves a mortgaged state across the move.)
2. **Properties the counter party gives** → `Transfer(engine, counterParty,
   proposer, property)` for each.
3. **Net money** → one `TransactionService.ProcessDealPayment(engine, proposer,
   counterParty, netAmount)`, where
   `netAmount = MoneyFromCounterParty − MoneyFromProposer` (signed from the
   proposer's perspective: positive = proposer receives). Pre-gated to cash so
   it never shortfalls (§11).
4. **Reservation break-through** → `CheckReservationRuleSetObtained` for **both**
   players (either side may complete a colour set through the deal, which ends
   the reserve mechanic for everyone — `game-rules.md` Reserved Properties; one
   of the four break-through paths, §12).
5. **Rent normalisation** → `PropertyService.NormaliseRentLevels(engine)` once,
   after all transfers.
6. **Receipts** fall out of the seams: `ProcessDealPayment` emits the two-
   perspective `FinancialTransactionReceipt`s (`Reason = Deal`); each `Transfer`
   emits the two-perspective `PropertyTransferReceipt`s (`Reason = Deal`).

No mid-deal `SaveChanges` — like every rule path, the deal's mutations
accumulate on the working copy and commit at the turn boundary
(`transactions.md` §7).

---

## 9. No Counter-Offers

The app deliberately does not model negotiation. A `DealPrompt` has exactly two
responses — **Accept** and **Decline** — and a decline ends the deal. To change
terms, the proposer starts a fresh deal (or, far more likely, the table agrees
the terms verbally first and the app records the final shape). This keeps the
prompt a clean binary and avoids a stateful offer/counter-offer machine the
helper model doesn't need.

---

## 10. Single-Writer Makes Validation Trivial

Because every deal runs on the game's single-writer executor
(`web-orchestration.md` §2), the game is **parked from the moment a deal is
proposed until it is accepted or declined** — nothing else mutates state in
between. This eliminates a whole class of time-of-check/time-of-use bugs:

- The counter party cannot spend their money, lose a property, or build/sell
  while the `DealPrompt` is open.
- So a deal **validated when it is built cannot go stale before it is applied**.
  Validate the contents once (ownership + dealability + each side's money ≤ that
  side's cash, no duplicates); on accept, just execute.

The trade-off is that **proposing a deal freezes the table** until it's answered
— consistent with how auctions and shortfall already park the pump, and
acceptable because the host can always answer on the counter party's behalf
(host-bypass), so a deal can never wedge waiting on an absent phone.

### Cursed deals are allowed

A player may agree to a terrible deal ("I give you £10, you give me everything
you own") and the engine applies it verbatim. Single-writer guarantees the
counter party genuinely owns what they're giving; fairness is a human judgement,
not the app's. Vetting or blocking lopsided deals is explicitly out of scope.

---

## 11. Funding — Discretionary, Paid From Cash on Hand

Deal money is **discretionary spending**, not a debt, so `game-rules.md` Default
rule 7 bars raising funds for it: a player cannot mortgage, sell buildings, or
take a loan to *fund* a deal. Concretely:

- Each side's money is **pre-gated to that side's current cash** in the builder
  (and re-validated on the writer thread). A deal whose money leg exceeds cash
  cannot be built.
- The apply-time `ProcessDealPayment` must therefore **never open a nested
  `ShortfallPrompt`**. `ProcessDealPayment` currently sets
  `allowShortfall: amount < 0` (shortfall allowed when paying) — for the deal
  path the money is already capped at cash, so the branch is unreachable; but a
  deal payment falling into a shortfall-inside-a-shortfall would be a bug. Treat
  the cash cap as the guarantee that keeps it unreachable.

This is the one place the *shortfall settlement* path (§7) is subtle but
consistent: the debtor is short on the *original* debt, but the *deal money*
they offer (≤ their cash) is affordable — the property leg covers the value gap,
and the debt cancellation is what makes the creditor whole.

---

## 12. Reserve-Rule Interaction — Don't Block, Check After

Per `game-rules.md` Reserved Properties, a complete colour set may be achieved
"through a deal." The decision:

1. **A deal is never blocked because the reserve rule is active.** Helper, not
   simulator — if the table agrees a deal that hands someone a set during the
   reserve phase, it goes through.
2. **The break-through is checked *after* accept**, for both players:
   `CheckReservationRuleSetObtained(proposerId)` and `(counterPartyId)` (§8.4).
   A deal that completes anyone's first full set ends the reservation mechanic
   for everyone — the first of the four break-through paths (the others:
   Free Parking, auction win, deadlock).

---

## 13. Prompt Types

Two new prompt types, catalogued in `choice-events.md` §15 when built. Both are
mid-execution prompts; the host may submit on the subject's behalf (host-bypass,
`PromptValidator`).

### `BuildDealPrompt` (engine → proposer; shortfall path only)
- **Purpose:** ask the debtor to construct a deal with a fixed counter party
  (the creditor) during a shortfall.
- **Fields:** `PlayerId` (the debtor / builder), `CounterPartyId` (fixed
  creditor), and whatever the builder needs (the debtor's dealable indexes, the
  creditor's dealable indexes, each side's cash cap).
- **Response (`BuildDealResponse`):** the full `DealContents` (§4).
- **Validation:** submitter is the debtor or host; every give-index is dealable
  for its owner; each money leg ≤ that side's cash; no duplicates.
- *Not used on the turn-boundary path* — there the Deal tab is the builder and
  the contents ride on the `ProposeDeal` command.

### `DealPrompt` (engine → counter party; both paths)
- **Purpose:** the accept/decline on the proposed deal.
- **Fields:** `PlayerId` (the counter party), and the `DealContents` plus the
  proposer id, server-authored, so the partial can render the "what you
  give / what you receive" summary (flipped to the counter party's perspective:
  swap the two lists, negate the money sign).
- **Response (`DealResponse`):** `Accept: bool`.
- **Validation:** submitter is the counter party or host; `Accept` is binary.
  Contents are not re-supplied, so nothing else to validate on the response.

---

## 14. Front-End — the Deal Builder

The builder is the front-end the engine's `DealContents` feeds. It surfaces in
two host contexts, reusing the one partial: the **Deal tab** of a player profile
(turn-boundary command) and the **`BuildDealPrompt`** render (shortfall path).
The accept/decline `DealPrompt` renders as a full-page player-profile prompt
(Approach A, server-rendered in the drawer — `session-notes` 2026-06-03 §11).

Per John's layout:

1. **Player list** — pick the counter party (turn-boundary path; fixed to the
   creditor on the shortfall path).
2. **Two deal sections** — a "you give" section and a "they give" section, each
   with a **money input** and a **property grid** below it.
3. **Property grid** — coloured squares, one per property, ordered **left→right
   as they appear on the board** and **top→bottom by `PropertySet` enum order**
   (brown, blue, pink, …), a new row per set. Clicking a square toggles the
   property into that side of the deal. Non-dealable properties (built-on /
   reserved) are shown disabled; mortgaged are shown dealable (with a mortgaged
   tint).
4. **Summary** — two blocks at the bottom, **"What you give:"** and **"What you
   receive:"**, listing the selections as property *badges* (with names) and a
   money badge — e.g. *What you give: ⟨Pentonville Road⟩ ⟨Vine Street⟩* /
   *What you receive: ⟨£600⟩ ⟨Old Kent Road⟩*.
5. **Buttons** — **Propose** (top) and **Cancel** (bottom). Cancel is purely
   client-side (nothing sent); Propose submits the command (tab) or the
   `BuildDealResponse` (shortfall).
6. **Counter party's view** — the same summary, flipped by context (their "what
   you give" is the proposer's "what you receive"), with **Accept** (top) and
   **Decline** (bottom).

---

## 15. Rule Citations

Deals were excluded from the initial citable `RuleCode` set. Building deals adds
`Deal_*` codes (+ `rules.json` entries), cited as the relevant branch fires —
e.g. the discretionary-funding rule (Default rule 7), the creditor-deal debt
discharge (Default rule 7 / `transactions.md` §6), and the reserve break-through
(Reserved Properties). Cite as built, per the standing convention
(`rule-citation.md` §11).

---

## 16. Open / TODO

1. **`DealService` core.** `RunDeal` (open `DealPrompt`, park, apply on accept)
   and the apply path (§8). `DealForShortfall` is currently a stub returning
   `true` and applying nothing.
2. **`BuildDealPrompt` / `DealPrompt`** + responses, validator branches, and
   `[JsonDerivedType]` discriminators (`choice-events.md` §15).
3. **Turn-boundary `ProposeDeal` command** — the rich command payload
   (`counterPartyId` + `DealContents`), the hub method, and the
   `PlayerProfileService` enqueue (gated by `CanDeal`, re-checked on the pump).
4. **`DealableProperties`** — the mortgaged-inclusive eligibility filter (§5);
   do not reuse `TradableProperties` verbatim.
5. **Front-end builder partial** (§14) and the `DealPrompt` full-page render.
6. **Decline notification** — whether the proposer gets an `AcknowledgePrompt`
   on a turn-boundary decline, or just learns via the state broadcast (§6.5).
7. **Who may propose (turn-boundary).** `CanDeal` permits any authorised actor at
   a turn boundary, not only the current player. Confirm that's intended, or
   narrow it.
8. **`Deal_*` rule codes** + `rules.json` entries (§15).

---

## 17. Traceability

1. **`game-rules.md`** — Default rule 7 (no raising funds for discretionary
   spend; a debt to another player may be settled by a deal that discharges it),
   Reserved Properties (a set may be completed via a deal; the break-through that
   ends the mechanic), Bankruptcy (the surrender alternative to settling).
2. **`choice-events.md`** — the prompt framework (§2 commands-vs-prompts; §15 is
   where `BuildDealPrompt` / `DealPrompt` are catalogued when built).
3. **`turn-state.md`** — the `CanDeal` gate (§3) and the turn-boundary windows a
   deal command fires from; the bilateral-reachability note (§9.4) is answered
   by host-bypass on the counter party's prompt.
4. **`transactions.md`** — `ProcessDealPayment` (the money leg) and the
   `DebtSettled` tri-state outcome (§6) the shortfall path returns.
5. **`auction-flow.md`** — the sibling mid-execution prompt loop that also parks
   the single-writer pump; the same parking model applies here.
6. **`web-orchestration.md`** — the single-writer executor (§2) that parks during
   a deal and the authoritative gate re-check on the pump (§5).
7. **Code** — `MP.GameEngine/Services/SubSystems/DealService.cs` (core),
   `MP.GameEngine/Services/SubSystems/ShortfallService.cs` (the `ProposeDeal`
   branch), `MP.GameEngine/Services/PropertyTransferService.cs` (`Transfer`),
   `MP.GameEngine/Services/TransactionService.cs` (`ProcessDealPayment`),
   `MP.GameEngine/Models/Snapshot/GameModel.cs`
   (`CheckReservationRuleSetObtained`, the `DealableProperties` filter),
   `MP.GameEngine/Services/SubSystems/PropertyService.cs`
   (`NormaliseRentLevels`).
