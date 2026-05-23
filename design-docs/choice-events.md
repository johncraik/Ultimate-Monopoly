# Choice Events — Design Notes

Initial notes on the framework for pausing the engine when it needs player
input before it can proceed. To be refined into firm decisions as the design
settles.

---

## 1. The Problem

The engine is command-driven: command in, new state + events out. But many
game situations require the engine to **stop and wait for a player decision**
before it can continue. The engine can't just return a final state — it needs
to say "I'm blocked, here are the options, answer me."

---

## 2. Situations Requiring a Choice

1. **Buy / Decline property** — landed on an unowned property.
2. **Auction bidding** — declined or unaffordable property goes to auction;
   all players may bid.
3. **Free Parking hand-in** — player chooses which eligible property to hand
   in, or which property to purge.
4. **NOPE card** — any player can interrupt with a NOPE; anyone can then NOPE
   the NOPE, chained without limit.
5. **Card with options** — cards can offer pick-1-of-N choices (e.g. "go to
   jail OR pay £500").
6. **Jail exit** — roll for a double, play a release card, or pay the fee.
7. **Loan vs mortgage** — player can't afford a payment and must choose how
   to raise funds.
8. **Building** — choosing where to build (which property in a set).
9. **Reserve vs buy** — the reserved-property mechanic.
10. **Voluntary bankruptcy** — "I quit" at any point during the player's turn.

All share a common shape: the engine reaches a point where it cannot proceed
without player input, presents the valid options, and resumes once a choice is
made.

---

## 3. Core Concept

The engine's return type expands from `(newState, events[])` to include an
optional **pending choice** that blocks further progress until answered.

```
Engine processes command
  -> produces new state + events
  -> OR produces new state + events + a ChoiceEvent (turn is suspended)

ChoiceEvent answered
  -> engine resumes with the answer, produces more state + events
  -> possibly another ChoiceEvent (e.g. NOPE chain, auction round)
```

---

## 4. Design Axes

### 4.1 Where does the choice live?

A pending choice must survive a server restart — it needs to be serialisable
and part of the persisted state. Options:

- A dedicated field on `GameCacheModel` alongside `Events`.
- A special kind of `EventReceipt` in the existing events list.
- Part of the turn-phase state (a phase like `AwaitingPlayerChoice`).

### 4.2 Single-player vs multi-player choices

Not all choices have the same shape:

- **Single-player** — the engine asks one player, that player responds
  (buy/decline, hand-in, jail exit, building, loan/mortgage).
- **Multi-player sequential** — each player acts in turn order (auction
  rounds).
- **Multi-player open** — any player can respond, potentially with a timeout
  (NOPE).

### 4.3 Choice validation

The engine defines what is valid (e.g. only properties the player is eligible
to hand in). The choice response is validated the same way any command is —
the engine rejects invalid answers and the choice remains pending.

### 4.4 Nesting / chaining

A choice answer can trigger another choice. Examples:

- NOPE chain — answering a NOPE opens another NOPE window.
- Buy property -> can't afford it -> loan or mortgage?
- Card option -> triggers a further decision.

The framework must handle arbitrarily deep chaining without special-casing
each combination.

---

## 5. Open Questions

1. Should the choice be **part of the turn-phase state machine** (a new phase
   like `AwaitingPlayerChoice`), or a **separate concept** that overlays onto
   any phase?
2. For NOPE and auction — timeout-based (real-time window) or explicit
   pass/bid from each player?
3. Should the choice carry full UI rendering info (message text, button
   labels), or just a typed discriminator so the web layer decides
   presentation?
4. How does a pending choice interact with the concurrency stamp — does
   answering a choice count as a state mutation that re-stamps?
5. How does the SignalR broadcast work — does the engine return "send this
   choice to player X" as part of its output, or does the web layer infer the
   target from the choice type?