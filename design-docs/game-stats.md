# Game Statistics — Event Persistence & On-Demand Projection

How the app turns the engine's per-turn event receipts into player statistics —
per-game ("most profitable set this game") and lifetime/cross-game — without a
brittle, pre-decided projection schema. Pairs with `event-receipts.md` (the
receipts this consumes) and `game-engine.md` §8–§9 (the snapshot timeline it
rides on).

**Status:** design, pre-implementation. The receipt framework
(`event-receipts.md`) is built; nothing in this doc is. This is the agreed plan.

---

## 1. Purpose & Scope

The engine already narrates every turn as a stream of `EventReceipt`s
(`event-receipts.md`). This doc covers **persisting that stream** and **deriving
statistics from it** — both a single game's stats and a player's lifetime stats
across all their games.

**In scope:** persisting the per-turn receipt stream, the on-demand projection
that combs it into summaries, and the per-game / lifetime query model.

**Out of scope:** the receipt taxonomy itself (`event-receipts.md`), live
in-game UI (the live view renders from the cache, not from stats —
`web-orchestration.md` §6), and the stat *screens* (a UI concern, designed when
built).

---

## 2. Two sources, two kinds of stat

Statistics split by where the data comes from:

1. **State-over-time** — cash, net worth, property count, loan total *at turn N*.
   These are **already in the snapshot**: each `GameSnapshot.StateJson` is the
   full `GameModel` at that turn, so a "money over time" graph is just the
   snapshot series read in turn order. **No events required.**

2. **Flow / attribution** — rent *earned*, fines paid, jail visits, doubles
   rolled, rent *per colour set*. Snapshots show balances, not *why* they
   changed; only the receipt stream carries the cause. **These need the
   receipts.**

This doc is about (2) — the flow/attribution stats and the summaries built from
them. Pure state-over-time graphs can read the existing snapshots directly and
need nothing here.

---

## 3. Why not a per-turn projection

The obvious first instinct — write a flat per-turn-per-player stat row
(`game-engine.md` §9's `GameTurnStats` sketch) — works for fixed, low-cardinality
metrics but collapses on attribute-sliced ones. "Most profitable **set**" would
need a column per (set × reason): *rent-from-brown, rent-from-blue,
rent-from-pink, …* — combinatorial, almost always null, and **every new slice is
a schema change decided up front, forever**.

So a per-*turn* projection is the wrong shape. Instead:

> **Persist the raw receipts; compute the slices on demand at query time.**

The axis that matters is not single-turn vs multi-turn (a per-turn row aggregates
into multi-turn stats fine) — it is **pre-defined metrics vs arbitrary
later-queries**. Keeping the raw receipts buys arbitrary later-queries: a slice
nobody thought of (per-property, per-counterparty, by-reason) is computable from
data already on the receipt, with no schema change and **retroactively for every
game already played** (§10).

---

## 4. The model — two layers

1. **Raw receipts, persisted per turn** (§5) — an append-only, durable record of
   "what happened", the input to every stat.
2. **Summaries, projected on demand** (§7) — per-game and per-player aggregates
   computed by combing the raw receipts when stats are *viewed*; never stored as
   pre-sliced columns, optionally cached.

The snapshot remains the source of truth (`event-receipts.md` §1). Persisted
receipts are a **derived log**, not authoritative state — they could in
principle be regenerated from snapshot diffs, but we write them at emission time
so we don't have to.

---

## 5. Persistence — `GameTurnEvents`

The per-turn receipt list is serialised to a blob, **one record per
`GameTurn`**, keyed to the turn the receipts **occurred in**.

1. **Storage shape.** A `GameTurnEvents` row (or column) 1:1 with `GameTurn`,
   holding `EventJson` (the serialised per-turn `EventReceipt[]`, polymorphic via
   the existing `[JsonDerivedType]` discriminators). A blob, not normalised rows
   — because the projection (§7) reads a whole game's events **once** and
   aggregates in memory; it never runs ad-hoc SQL over individual receipts, so
   the queryability of normalised rows buys nothing and the blob is simpler.

2. **A separate table, not a column on `GameSnapshot`.** Receipts are
   **end-of-turn**; snapshots are **start-of-turn** (§6). Bolting `EventJson`
   onto the snapshot row would either force an off-by-one (snapshot N carrying
   turn N-1's events) or mutate an already-written, meant-to-be-immutable
   snapshot row. A separate record keyed to the turn the events belong to avoids
   both.

3. **Written before `ClearEvents`, in the snapshot transaction.** Receipts live
   on `GameCacheModel.Events` and are cleared at the turn boundary
   (`TurnStateProvider.TransitionToNextPlayer` / `TransitionToExtraTurn` call
   `cache.ClearEvents()`). The persistence must capture `cache.Events` **before**
   that clear and write it in the **same transaction** as the boundary's snapshot
   (so the event log and the snapshot timeline commit atomically). This needs a
   new seam — extend `ISnapshotService`, or add an event-store the transition
   calls alongside `CreateSnapshotAsync`.

4. **Restart consistency falls out for free.** Because the blob commits with the
   turn-boundary snapshot, a mid-turn restart loses the in-progress turn's
   receipts exactly as it loses the in-progress turn itself (re-rolled from the
   last snapshot — `choice-events.md` §1). Every *committed* turn's receipts are
   durable, and the persisted log always matches the committed snapshot timeline.

5. **The final turn needs an explicit flush.** Events for turn N are normally
   captured at the N→N+1 boundary (before that boundary's `ClearEvents`). The
   game's **last** turn has no successor boundary, so its receipts must be
   flushed when the game is finished/cancelled. Open item — see §11.

---

## 6. The timing — why a separate record

`game-engine.md` §8: the snapshot is taken at the **start** of each turn (it
captures the state the turn begins from). Receipts accumulate **during** the turn
and are cleared at its **end**. So for a given `GameTurn`:

- `GameSnapshot` = the **start** state, written at the turn-open boundary.
- `GameTurnEvents` = the receipts **during** the turn, written at the turn-close
  boundary (the next turn's snapshot transaction, before `ClearEvents`).

Same `GameTurn`, two different commit points. That asymmetry is exactly why the
events get their own record rather than sharing the snapshot row.

---

## 7. On-demand projection — the stats service

Stats are **computed on demand**, not materialised each turn:

1. **Comb on view.** When a game's (or player's) stats are requested, the stats
   service loads the relevant `GameTurnEvents` blobs, deserialises, and
   aggregates in memory into the requested summary. Data volume is tiny (dozens
   to low-hundreds of turns × a few players), so a full re-comb per request is
   cheap; cache the result if a hot screen warrants it.

2. **No incremental upkeep, never stale.** On-demand means there is no per-turn
   stat-maintenance step to keep correct, and the numbers always reflect the
   current receipt log. It also isn't limited to finished games — a mid-game
   "so far" stat is the same comb over the turns played to date.

3. **The summary can be wide — it is per-game / per-player, not per-turn.** The
   combinatorial-column problem (§3) only bit at *per-turn* granularity. A
   per-game-per-player summary holding a full per-set rent breakdown is a handful
   of rows per game — trivial. So the summary record (computed, or a cached
   materialisation) may carry whatever shape reads best.

---

## 8. Per-game example — Most Profitable Set

No pre-sliced columns; the comb derives the slice from data already on the
receipt:

```
rent earned by player P from set S (one game)
  = Σ FinancialTransactionReceipt.Amount
    where PlayerId == P
      and Reason == Rent
      and Amount > 0                                   // received, not paid
      and PropertySetHelper.ResolveSet(CounterpartyPropertyIndex) == S
```

`FinancialTransactionReceipt` already carries `Reason`, `Amount`, and
`CounterpartyPropertyIndex`, and `TransactionService.EmitReceipts` emits **both
perspectives** (the owner gets the positive `+rent` mirror with the property
index), so owner-side attribution by property → set is fully present.
"Most profitable set" is the argmax over `S`. Any other slice — per-property,
per-counterparty, fines-paid, jail-fee total — is the same shape against the same
blobs, no schema change.

---

## 9. Lifetime / cross-game stats — finished games only

A player's **overall** stats aggregate across games, but **only finished games
count** — an in-progress game's running totals must not pollute lifetime figures.

- The scope is enforced by the game's state, reached via the existing
  relationship `GameTurnEvents → GameTurn → Game`: include a game's events only
  when `Game.State == Finished` (and/or `GameOutcome` is set). No separate
  per-record flag is needed — the nav to `Game` is the single source of the
  in-play/finished distinction.
- Lifetime stats are the same on-demand comb (§7) over the union of the player's
  **finished** games' `GameTurnEvents`.

(Whether cancelled/abandoned games count toward lifetime stats is a rule
decision — see §11.)

---

## 10. Stats are only as complete as the receipts

Every stat is downstream of the receipts, so a flow that doesn't emit a receipt
is invisible to stats. Several effects don't emit yet (cards, deals, Free Parking
payout, tax — stubbed/unbuilt), so flow stats are accurate for the built paths
(rent, loans, mortgage fee, auction) and fill in as those subsystems land.

Two consequences:
1. **Each new effect must emit its receipt as it's built**, or the stat silently
   under-counts. This is a standing review check (`event-receipts.md` §8
   producer convention).
2. **Improving the receipts retroactively improves every stat** — because we
   store raw events and project on demand, there are no projection columns to
   backfill; a richer receipt stream just yields richer stats for every game
   played after the change.

---

## 11. Open / TODO

1. **Final-turn flush.** Capturing turn N's events at the N→N+1 boundary leaves
   the last turn's receipts unflushed (§5.5). Decide the flush point on
   game-finish/cancel (likely the same path that sets `Game.State = Finished`).
2. **Caching policy.** On-demand comb is cheap, but a frequently-viewed lifetime
   screen may warrant a cached/materialised summary (invalidated when a new game
   finishes). Decide if/when to add it — start without.
3. **Cancelled games in lifetime stats.** §9 counts finished games; whether
   `Cancelled` games contribute is a rules call.
4. **State-over-time graphs.** Money/net-worth-over-time can read the snapshot
   series directly (§2.1); decide whether that path is built here, left to a
   later snapshot-reader, or materialised as the `game-engine.md` §9
   `GameTurnStats` rows. Not required for the flow stats this doc centres on.
5. **`event-receipts.md` amendment (deferred).** §6/§9 there state receipts are
   in-memory only, lost on restart, and "don't need to be persisted." This doc
   adds a persisted derived log; the snapshot stays the source of truth. Update
   that doc when this is built — **not yet**.
6. **Persistence seam.** Exact shape of the write path (extend `ISnapshotService`
   vs a dedicated event-store invoked by the transitions) — pin during build,
   keeping it inside the turn-boundary transaction (§5.3).

---

## 12. Traceability

1. **`event-receipts.md`** — the receipt taxonomy, the `FinancialTransactionReceipt`
   fields the projection reads, and the producer/consumer conventions; this doc
   is the persistence + projection layer it anticipated (§6/§9 there pending the
   amendment in §11.5).
2. **`game-engine.md` §8–§9** — the snapshot timeline this rides on, the
   start-of-turn snapshot timing (§6), and the `GameTurnStats` projection idea
   superseded for flow stats by the raw-events model here.
3. **`turn-state.md`** — `TransitionToNextPlayer` / `TransitionToExtraTurn` are
   the boundaries where `ClearEvents` fires and the per-turn blob must be
   captured (§5.3).
4. **`game-rules.md`** — the rules behind the slices (rent, sets, fines, jail,
   loans) that give the stats meaning.
5. **Code (when built)** — `Models/DataModels/Games/GameTurnEvents.*`,
   `Services/GameEngine/SnapshotService.cs` (or a new event-store), the engine
   `GameCacheModel.Events` / `ClearEvents` seam, and the web-side stats service.


---

---

## 13. Planned Player Stats (per game):

- Money Earned
- Money Spent
- Amount spent buying properties
- Amount spent on building
- Amount spent on fines
- Amount spent to leave Jail
- Amount spent on loans
- Maximum number of sets obtained
- Most profitable property
- Least profitable property
- Most profitable set
- Least profitable set
- Number of cards drawn
- Number of cards kept
- Number of cards played (after keeping)
- Times landed on free parking,
- Times landed on tax,
- Times landed on GO,

List is TODO/WIP
