# Event Receipts — State-Change Log & Stats Source

The engine's after-the-fact narrative trail. Every meaningful state change
in a turn produces a receipt; the per-turn stream of receipts is the input
to game history and the (future) statistical-snapshot projection. It does
**not** drive the live UI — that renders from the whole-cache broadcast
(`web-orchestration.md` §6). Pairs with the prompt framework
(`choice-events.md`) and the turn-state provider (`turn-state.md`) to round
out the foundation layer.

**Status:** built and in use. The taxonomy (§3), the enriched
`FinancialTransactionReceipt` (§4), the `IEventEmitter` seam (§5), and the
base-field bookkeeping (§7) are landed in `MP.GameEngine`, and the rule
services emit through the seam throughout. See the drift note at the foot of
this document.

---

## 1. What a Receipt Is

A **receipt** is an immutable, atomic, after-the-fact record of one
state change. The engine emits one as a side-effect of every mutation
that matters for narration or stats. The snapshot remains the source of
truth — receipts are *derived narrative*, not authoritative state.

1. **Immutable.** Once written, never edited. A correction is a new
   receipt, not a mutation of an old one.
2. **Atomic.** One receipt represents one discrete change. A logical
   action (e.g. buying a property) may emit several — money out,
   ownership transfer — but each receipt is independently meaningful.
3. **After-the-fact.** Emitted *after* the mutation succeeds, never
   speculatively. A rolled-back operation produces no receipts.
4. **Ordered.** The order in the per-turn list is the order things
   happened. Consumers can reconstruct narrative from the sequence.
5. **Not the source of truth.** The snapshot is. Receipts can be
   regenerated from snapshot diffs in principle, but in practice we
   write them at emission time so we don't have to.

Receipts are the engine's *output channel for "what happened"*, parallel
to how prompts are its *input channel for "I need to know X"*.

---

## 2. The Guiding Principle — Semantic-Flavoured, Primitive-Rich

Two ends of a spectrum:

- **Pure primitives** — one receipt type per low-level mutation
  (BalanceChanged, PropertyOwnerChanged, BoardIndexChanged…). Stats and
  UI must encode the rules to interpret them.
- **Pure semantics** — one receipt type per logical action
  (PlayerBoughtProperty, PlayerPaidRent, PlayerLeftJail…). Big surface,
  no rule logic in consumers.

We sit toward the **semantic end, but each receipt carries
primitive-rich content** so the stats projection can run aggregations
without re-implementing rules.

> A receipt's *type* answers "what kind of thing happened?" — its
> *fields* answer "what does the stats projection / UI need to know?"

For example, `FinancialTransactionReceipt` is a semantic *type* ("money
moved") with rich primitive content (`Amount`, `Reason`, source/target
attribution). Stats can directly sum-by-reason or group-by-property; the
UI can render "Player X paid £150 rent to Player Y for Pall Mall"
without engine knowledge.

This split keeps the receipt surface small while making the per-receipt
payload powerful enough that consumers don't need to know the rules.

---

## 3. Receipt Taxonomy

The set below is the agreed shape. Existing receipts that don't appear
here are dropped (see "Drop"); new ones are flagged "*Add*".

### 3.1 Roll & Movement

| Receipt | Purpose | Stats use |
|---|---|---|
| `DiceRollReceipt` | Records the dice values rolled | Doubles/triples counts, sum stats, frequency |
| `PlayerMovedReceipt` | Records a board-position change with direction and start/end indexes | Distance travelled, path coverage |
| `PlayerDirectionChangedReceipt` | Records a direction flip | Direction-change frequency |
| `PlayerSwappedReceipt` | Records two players swapping positions (card effect) | Swap frequency |

### 3.2 Money & Properties

| Receipt | Purpose | Stats use |
|---|---|---|
| `FinancialTransactionReceipt` | Records a money movement. Carries `Amount`, `Reason`, source/destination | Most money earnt, rent stats per property, fine totals, all money flow |
| `PropertyTransferReceipt` | Records a property ownership change. Carries direction (acquire / lose), count, and a `PropertyTransferReason` | Acquisition rate, property turnover, attribution by reason |

### 3.3 Jail

| Receipt | Purpose | Stats use |
|---|---|---|
| `PlayerEnteredJailReceipt` *(Add)* | Records a player being sent to jail (any cause: 3 doubles, Go-To-Jail space, card) | Times in jail per player |

Leaving jail decomposes into a `FinancialTransactionReceipt`
(`Reason=JailFee`, if paid) and a `PlayerMovedReceipt` (out of jail). No
separate `PlayerLeftJail` receipt is needed.

### 3.4 Cards

| Receipt | Purpose | Stats use |
|---|---|---|
| `CardTakenReceipt` | Records a card being added to a player's hand | Card-acquisition frequency by type |
| `CardPlayedReceipt` | Records a card being played from a hand | Card-usage frequency, effect-tracking |

### 3.5 Terminal Player Events

| Receipt | Purpose | Stats use |
|---|---|---|
| `PlayerBankruptedReceipt` | Records bankruptcy with cause (voluntary/forced) and shortfall context | Game-end attribution, bankruptcy rate |

### 3.6 Drop

These exist today but should be removed — they're redundant with the
above:

- **`PlayerLeftJailReceipt`** — decomposes into `FinancialTransaction
  (Reason=JailFee)` + `PlayerMoved`. Nothing semantic to preserve.
- **`FreeParkingReceipt`** — decomposes into one or two
  `FinancialTransactionReceipt`s with `Reason=FreeParkingPay` /
  `FreeParkingTake`. The initial/final pot amount is derivable from the
  receipt stream and from the snapshot.
- **`EventReceiptType` enum** — duplicates the `JsonDerivedType`
  discriminator list and isn't referenced anywhere. Dead code.

---

## 4. `FinancialTransactionReceipt` — The Workhorse

This is the receipt most stats will query, so its shape matters more
than the others.

```csharp
public sealed class FinancialTransactionReceipt : EventReceipt
{
    public long Amount { get; init; }                  // signed: negative = pay, positive = receive
    public FinancialReason Reason { get; init; }
    public TransactionDestination Destination { get; init; }
    public string? DestinationPlayerId { get; init; }  // when Destination = Player
    public ushort? SourcePropertyId { get; init; }     // BoardIndex of the property, when relevant
}

public enum FinancialReason
{
    Rent,            // landed-space rent (uses SourcePropertyId)
    Tax,             // tax space
    Fine,            // card or rule fine
    GoBonus,         // passing or landing on GO
    JailFee,         // paying out of jail
    FreeParkingPay,  // paying into Free Parking
    FreeParkingTake, // taking from Free Parking
    LoanTake,        // money in from a loan
    LoanRepay,       // money out for loan repayment
    Purchase,        // buying a property
    Auction,         // winning an auction (money out)
    Build,           // building houses/hotels
    Sell,            // selling houses/hotels
    Mortgage,        // mortgaging (money in)
    Unmortgage,      // unmortgaging (money out)
    CardPayout,      // card-driven money in
    CardCharge,      // card-driven money out
    Deal,            // money component of a player-to-player deal
}
```

1. **Sign convention.** The receipt is from the **subject player's**
   perspective: positive = received, negative = paid. The
   `DestinationPlayerId` field is the *other* side of the transaction
   (the payer when the subject received; the payee when the subject
   paid).
2. **`Reason` is the categorical axis.** All stats group by Reason —
   "total rent earnt per player", "money lost to fines", "loan totals".
3. **`SourcePropertyId` is property attribution.** Set for any
   transaction where a property is the cause (`Rent`, `Purchase`,
   `Build`, `Sell`, `Mortgage`, `Unmortgage`); null otherwise. Lets
   stats group by property without inferring rules.
4. **One receipt per movement.** A buy is `FinancialTransaction(Amount=-£60,
   Reason=Purchase, SourcePropertyId=…)` *plus*
   `PropertyTransfer(…)`. A two-leg flow (e.g. a deal with money +
   property both ways) emits multiple receipts.

---

## 5. The `IEventEmitter` Seam

Rule services emit through a seam, not by reaching into the cache
directly — same shape as `IPromptProvider`. Improves testability and
keeps emission semantics in one place.

```csharp
public interface IEventEmitter
{
    void Emit(EventReceipt receipt);
}

internal sealed class EventEmitter(GameCacheModel cache) : IEventEmitter
{
    public void Emit(EventReceipt receipt) => cache.AddEvent(receipt);
}
```

1. **Single method, no batching primitive.** Multi-receipt logical
   actions emit one call per receipt — the natural order in the cache
   list is the narrative order.
2. **`AddEvent` re-stamps `ConcurrencyStamp`** (already implemented), so
   each emission invalidates stale client views.
3. **Rule services depend on `IEventEmitter`, never on
   `GameCacheModel.AddEvent` directly.** This is the convention to hold
   on review.

---

## 6. Lifecycle

> **Updated 2026-05-28 — receipts no longer drive the live view.** This section
> (and §8) originally described receipts as the source of live UI narration via a
> per-receipt SignalR push. That role is gone: the live view renders from the
> **whole `GameCacheModel`** broadcast by `IEngineNotifier.StateChanged` (see
> `web-orchestration.md` §6), and `Events` is `[JsonIgnore]`d out of that frame.
> Receipts are now **internal history + the stats-projection input only** — no
> live-broadcast stage. The text below reflects that.

| Stage | Where | When |
|---|---|---|
| Emission | `IEventEmitter.Emit(...)` → `cache.AddEvent(...)` | Immediately after the corresponding state mutation succeeds |
| Per-turn clearing | `cache.ClearEvents()` called by `TurnStateProvider.TransitionToExtraTurn` and `TransitionToNextPlayer` | Turn boundary |
| Stats projection (future) | Web layer reads the per-turn list at the turn boundary, *before* clearing | Turn boundary |
| Restart | Lost (cache is in-memory only) | At server restart, mid-turn |

Receipts are **not** broadcast live. The live frame is the cache itself
(`web-orchestration.md` §6); clients learn "what happened" by diffing the
state they're pushed, not by replaying receipts.

1. **Per-turn scope.** Receipts live on the cache for exactly one turn;
   the next turn starts with an empty list. This matches
   `game-engine.md` §8 — events are not part of the persisted snapshot.
2. **Lost on restart.** Consistent with the prompt framework's restart
   contract (`choice-events.md` §1) — a turn interrupted by restart is
   re-rolled from the previous snapshot, so its receipts go with it. No
   data integrity issue because the snapshot is canonical.
3. **Stats projection emission is separate.** When stats become a
   feature (see `game-engine.md` §9), the projection writes a flat row
   per player per turn *alongside* the snapshot at turn-boundary commit.
   The stats writer consumes the receipt stream for the turn that just
   ended, *then* the receipts are cleared. Receipts themselves don't
   need to be persisted for stats to work.

---

## 7. Base Fields

```csharp
public abstract class EventReceipt
{
    public string PlayerId      { get; init; } = "";  // the subject — producer sets
    public uint   TurnNumber    { get; internal set; } // assigned by cache.AddEvent
    public ushort SequenceIndex { get; internal set; } // assigned by cache.AddEvent
}
```

1. **`PlayerId` is the subject.** Same naming convention as prompts
   (`Prompt.PlayerId`). The player the receipt is *about* — usually the
   player whose state changed. For events with two participants
   (`FinancialTransaction`, `PlayerSwapped`), the subject is the one
   whose perspective the receipt is from; the counterparty lives in a
   typed field on the concrete receipt. Producer-set via `init`.
2. **`TurnNumber`** — read off `cache.Game.Metadata.TurnNumber` at emission
   time. Useful when receipts are broadcast or persisted outside the
   per-turn cache list.
3. **`SequenceIndex`** — position in the per-turn list at the moment of
   emission, derived from `cache.Events.Count`. Explicit ordering, insurance
   against list-order assumptions on serialised payloads.

`TurnNumber` and `SequenceIndex` use `{ get; internal set; }` rather than
`init` because they're assigned *after* construction by
`GameCacheModel.AddEvent` — `init` is "construction-time only", which
wouldn't allow the framework to backfill them. The `internal` modifier
restricts mutation to the `MP.GameEngine` assembly so producers can't set
them by mistake.

---

## 8. Producer / Consumer Conventions

### Producer convention

Rule services emit receipts as the **last step** of a successful
mutation:

```csharp
// Pattern inside a rule service method:
// 1. Validate
// 2. Mutate the game state (update player.Money, property.OwnerId, etc.)
// 3. Emit receipt(s) for what changed
```

1. **Emit after, never before.** A receipt represents a fact, not an
   intention. If the mutation throws, no receipt — the cache is
   transactional via `_working`.
2. **Multi-receipt actions in deterministic order.** A buy emits
   `FinancialTransaction` first, then `PropertyTransfer` — the order
   reads naturally as narrative ("paid £60, now owns Pall Mall"). Stick
   to a convention so replay UI is consistent.
3. **One receipt per logical change, not per field touched.** Setting
   `player.Money -= 60` does not emit; the higher-level action ("paid
   for property") does.

### Consumer convention

There are two consumers and they each treat the stream differently:

1. **Stats projection (future).** Reads the per-turn list at turn
   boundary, computes deltas (e.g. `+Amount` for player money earnt),
   writes a flat row per player per turn. The receipt stream is the
   input; persisted stats are the output.
2. **Replay UI (future).** If we want true replay, we'd need persisted
   receipts. Currently we don't — replay can step through the snapshot
   timeline instead. Receipts are *not* the replay source.

**The live UI is *not* a receipt consumer.** It renders from the whole
`GameCacheModel` pushed by `IEngineNotifier.StateChanged`
(`web-orchestration.md` §6), and `Events` is `[JsonIgnore]`d out of that
frame entirely. Receipts never reach the client over the live channel.

---

## 9. Open / TODO

1. ~~**Broadcast cadence.** Is SignalR push per-receipt or
   per-turn-boundary?~~ Moot — receipts are not broadcast at all. The
   live channel pushes the whole cache (`web-orchestration.md` §6); the
   only future receipt consumer is the turn-boundary stats projection.

2. **Tests.** No tests on receipt emission yet (the framework piece —
   `EventEmitter.Emit` forwards correctly, `cache.AddEvent` backfills
   `TurnNumber` and `SequenceIndex`, sequence increments per emission).
   Add when the rule-services test suite goes in; rule-service tests
   will mock `IEventEmitter` (which is the testability case for the
   seam).

3. **`TransactionDestination` vs `FinancialReason` overlap.** Worth a
   pass to confirm the two enums don't encode the same information
   twice. `Destination` is "where did the money go (Bank / FreeParking /
   Player)"; `Reason` is "why did it move (Rent / Tax / etc.)". These
   seem orthogonal but some combinations are nonsense (e.g. `Rent` ↔
   `Destination=Bank` shouldn't happen). Could either trust producers
   or add validation.

---

## 10. Cross-References

- **`game-engine.md`** — surrounding engine architecture; §9 outlines
  the stats projection that consumes this receipt stream.
- **`game-rules.md`** — the rules that drive what mutations happen and
  therefore what receipts emit.
- **`choice-events.md`** — the prompt framework; receipts are the
  *output* counterpart to prompts' *input*. Restart-contract carries
  over (per-turn ephemeral, lost on restart).
- **`turn-state.md`** — `TransitionToExtraTurn` and
  `TransitionToNextPlayer` are the boundaries at which the per-turn
  receipt list clears.
- **`MP.GameEngine/Models/EventReceipts/`** — the concrete receipt
  types described above.
- **`MP.GameEngine/Enums/FinancialReason.cs`** — the categorical axis
  for `FinancialTransactionReceipt`.
- **`MP.GameEngine/Abstractions/IEventEmitter.cs`** — the producer seam.
- **`MP.GameEngine/Services/Framework/EventEmitter.cs`** — the concrete
  emitter, forwards to `cache.AddEvent`.

---

## Implementation status & drift

> This document records the **agreed design**, not the live state of the code.
> Since it was written the implementation has moved on — much of what is
> described here is built, and some details have changed. Any status, "TODO",
> "not yet built", or "pre-implementation" note above may be out of date.
>
> Where this doc and the code disagree, the **code (and the developer) win**
> (`docs/development/README.md`). Verify specifics against the current code.