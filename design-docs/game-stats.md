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
them. State-over-time figures are folded into the same per-game summary too.

> **Decision (2026-06-09):** §2.1 originally said state-over-time graphs "read the
> snapshots directly." Superseded — the **stat view/projection reads only
> `PlayerGameStat`, never the raw snapshots or events** (§12). State-over-time
> scalars (peak net worth, max sets) and any graph *series* are computed once at
> game conclusion and **persisted into `PlayerGameStat`** (a series as a
> serialised array field). When a game ends, that is all the data there will ever
> be — so we collate everything we need once and read the summary thereafter.

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

> **Decision (2026-06-09):** the per-game-per-player summary is **materialised
> into a `PlayerGameStat` record at game conclusion** (§12), not re-combed on
> every view. The comb described here is still the engine of the projection — it
> just runs **once per game** (in a background job at conclusion, with a recurring
> safety-net), and its output is persisted. The §3 "compute on demand" stance was
> aimed at not building a *per-turn* projection; a per-game materialisation is the
> §7.3-blessed wide summary, and materialising it makes **lifetime** stats a cheap
> aggregate over `PlayerGameStat` rows (§9) instead of re-combing every finished
> game's blobs on each profile view.

Stats are **combed once, then materialised**, not maintained per turn:

1. **Comb at conclusion.** When a game concludes, the stats service loads that
   game's `GameTurnEvents` blobs (flow) and `GameSnapshot`s (state-over-time),
   deserialises, and aggregates in memory into one `PlayerGameStat` per player.
   Data volume is tiny (dozens to low-hundreds of turns × a few players), so the
   comb is cheap; the view layer then reads only the persisted summary.

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

## 9. Lifetime / cross-game stats — concluded games only

A player's **overall** stats aggregate across their games, but **only concluded
games count** (Winner or Drawn — see §11) — an in-progress game's running totals,
and a cancelled game's partial data, must never pollute lifetime figures.

- **Lifetime = aggregate over `PlayerGameStat` rows** (§12), not a re-comb of raw
  events. A player's lifetime view is a `SUM` / average / argmax over their
  `PlayerGameStat` rows for games whose `GameOutcome ∈ {Winner, Drawn}` — O(games),
  not O(games × turns). This is the decisive reason for materialising (§7).
- **Scope** is the game's outcome, reached via `PlayerGameStat → Game`: a row only
  exists for a concluded (Winner/Drawn) game, because the projection only runs on
  conclusion (§11). Cancelled games produce **no** `PlayerGameStat` rows, so they
  are excluded for free — no filter needed at the lifetime layer.
- **Per-game averages** (e.g. "avg rent earned per game", "avg sets held") are the
  same aggregate divided by the player's concluded-game count.

---

## 10. Stats are only as complete as the receipts

Every stat is downstream of the receipts, so a flow that doesn't emit a receipt
is invisible to stats. Some effects don't emit yet (the **cards** subsystem is
unbuilt; `CardTaken` / `CardPlayed` / `PlayerSwapped` are declared but not yet
emitted, and `PlayerEnteredJailReceipt` is declared but currently **not wired** —
an engine bug to fix). Tax, Free Parking pay/take, deals, mortgage, loans, and
rent **do** emit today (confirmed in real game data). So flow stats are accurate
for the built paths and fill in as the rest land.

Two consequences:
1. **Each new effect must emit its receipt as it's built**, or the stat silently
   under-counts. Standing review check (`event-receipts.md` §8 producer convention).
2. **Improving the receipts retroactively improves every stat** — because the raw
   `GameTurnEvents` blobs are the durable source and `PlayerGameStat` is a derived
   materialisation, a richer receipt stream (or a fixed comb) just needs a
   **rebuild** of the affected rows (§12, `StatsVersion`); no projection columns to
   hand-backfill.

---

## 11. Game conclusion modes — when stats are projected

An in-play game ends one of three ways. Stats project for two of them:

- **A — Winner.** A single solvent player remains (last-player-standing via
  bankruptcy, or a host "declare winner" round-the-table). `GameOutcome.Winner`.
  **Stats projected.** The winner's `PlayerGameOutcome = Winner`, everyone else
  `Loser`.
- **B — Draw.** A soft, counted end while >1 player is still solvent (host
  "declare a draw" / everyone agrees to stop). `GameOutcome.Drawn`. **Stats
  projected.** All remaining players' `PlayerGameOutcome = Drawn`; their lifetime
  draw count increments. It counts as a real, finished game.
- **C — Cancel.** An abrupt end, there and then — `GameState.Cancelled`. **No
  stats, no winner, no draws.** Finished forever, contributes nothing to per-game
  screens or lifetime aggregates.

So the projection job (§12) fires only on A and B (i.e. `GameOutcome` is set to
`Winner` or `Drawn`); the cancel path skips it entirely.

---

## 12. `PlayerGameStat` — the materialised read model + projection job

**One `PlayerGameStat` record per player per concluded game**, holding every
metric in §13 as flat fields (plus serialised arrays for graph series). It is the
**only** thing the stat views read — never the raw snapshots or `GameTurnEvents`.

### 12.1 The model
- `PlayerGameStat` — 1:1 with (`Game`, player). FK to `Game` and the player
  (`UserId` / `GamePlayer`). Holds: the §13 scalar fields; serialised
  series fields for graphs (e.g. `MoneyOverTimeJson`, `NetWorthOverTimeJson`); and
  a **`StatsVersion`** int stamping the comb-logic version that produced it.
- `StatsVersion` is the rebuild lever: when the comb logic changes or receipts get
  richer (§10.2), bump the constant and the recurring job (12.3) recomputes any
  row below the current version. The `GameTurnEvents` + `GameSnapshot` blobs remain
  the durable source the rebuild reads from.

### 12.2 The projection — `GameStatsService.ComputeForGameAsync(gameId)`
- Loads the game's `GameTurnEvents` (flow attribution) **and** `GameSnapshot`s
  (state-over-time: peak net worth, max sets, the graph series), combs in memory
  (§7), and **upserts** one `PlayerGameStat` per player, stamped with the current
  `StatsVersion`.
- Pure and idempotent: read-only over committed game data, write-only over
  `PlayerGameStat`. Safe to re-run (rebuild) any time. It reads from the **DB**, not
  the engine cache — which is fine (and necessary), since `ConcludeGame` evicts the
  cache, and the final turn's events are already persisted by
  `TransitionToFinalTurn`.

### 12.3 Two entry points, one job (Hangfire — already wired)
- **Fire-and-forget on conclusion.** `GameCompletionService.ConcludeGame`'s stat
  TODO (currently `GameCompletionService.cs:~60`) enqueues
  `ComputeForGameAsync(gameId)` as a background job **after** the finished-game
  transaction commits (so the job reads committed data), then completion proceeds
  and broadcasts `GameCompleted` without waiting on the stats. Matches the existing
  Hangfire usage (`AddHangfireSqlServer`, the `IBackgroundJob` / `AddHangfireJob<T>`
  pattern, `PresenceFlushJob` as the template).
- **Recurring safety-net** — a `MissingGameStatsJob` (recurring, same
  `AddHangfireJob<T>` cron pattern) finds concluded games (`GameOutcome ∈
  {Winner, Drawn}`) whose players have no `PlayerGameStat`, or a row below the
  current `StatsVersion`, and computes/rebuilds them. Catches a failed
  fire-and-forget and version bumps alike.

### 12.4 Reads
- **Per-game stat screen** (the Finished page's "View My Game Stats", currently a
  no-op) → that game's `PlayerGameStat` rows.
- **Lifetime / profile** → aggregate the player's `PlayerGameStat` rows over their
  concluded games (§9).

---

## 13. Player stat catalogue (per game)

The full set to collate into `PlayerGameStat` — **deliberately exhaustive**; some
will be dropped at build time. Each is per-player-per-game. Unless noted, the
source is `FinancialTransactionReceipt` keyed by `PlayerId` (subject), with
property attribution via `CounterpartyPropertyIndex → PropertySetHelper.ResolveSet`.
Reasons are `FinancialReason` values. `[snapshot]` = derived from the
`GameSnapshot` series, not events. `[needs receipt]` = blocked until that receipt
is emitted (cards / jail / swap — §10).

### 13.1 Headline cash flow
- **Money earned (gross)** — Σ `Amount` where `Amount > 0`. [X]
- **Money spent (gross)** — Σ |`Amount`| where `Amount < 0`. [X]
- **Net cash flow** — Σ `Amount` (earned − spent). [X]
- **Largest single payment** / **largest single receipt** — min / max `Amount`
  (with reason + property for flavour). [X]
- **Largest rent payment** — property only, max, `Rent` where `Amount < 0`. [X]

### 13.2 Spending breakdown (Σ |`Amount`| where `Amount < 0`, by reason)
- **Spent acquiring property** — `Purchase` + `Auction` (reserve is tagged
  `Purchase` by the engine) + `UnReserve`. [X]
- **Spent on building** — `Build`. [X]
- **Spent unmortgaging** — `Unmortgage`. [X]
- **Spent on fines / penalties** — `Tax` + `MortgageFee` + `FreeParkingPay` +
  `CardCharge` (everything that isn't buy / unmortgage / unreserve / build / rent;
  jail and loans broken out separately below). [X]
- **Spent to leave jail** — `JailFee`. [X]
- **Spent repaying loans** — `LoanRepay`. [X]
- **Rent paid** — `Rent` where `Amount < 0`. [X]
- **Money given in deals** — `Deal` where `Amount < 0`. [X]

### 13.3 Income breakdown (Σ `Amount` where `Amount > 0`, by reason)
- **Rent earned** — `Rent` where `Amount > 0`. [X]
- **GO bonuses collected** — `GoBonus` (+ count of crossings). [X]
- **Building sell-backs** — `Sell`. [X]
- **Mortgage payouts** — `Mortgage`. [X]
- **Free Parking takings** — `FreeParkingTake`. [X]
- **Triple bonuses** — `TripleBonus`. **Snake-eyes bonuses** — `SneakEyes`. [X]
- **Dice-number bonuses** — `DiceNumBonus` where `Amount > 0`. [X]
- **Loan disbursements** — `LoanTake` (flag: this is debt, not true income). (exclude, covered in loan breakdown)
- **Card payouts** — `CardPayout` `[needs receipt]`. (deferred for now until cards are implemented)
- **Money received in deals** — `Deal` where `Amount > 0`. [X]
- **Money from bankrupt players** — `BankruptedPlayer`. [X]

### 13.4 Property & set economics (the marquee stats)
Per-property **net profit** (the agreed accurate formula), attributed by
`CounterpartyPropertyIndex`:
```
profit(P) = (rent earned on P + sell on P + mortgage payout on P)
          − (purchase/auction on P + unreserve on P + build on P + unmortgage on P)
```
- **Most profitable property** / **Least profitable property** — argmax / argmin
  of `profit(P)` (least can be negative — bought, never paid off). [X]
- **Most profitable set** / **Least profitable set** — same, summed over
  `ResolveSet`'s indexes. [X]
- **Rent earned per set** — full per-set breakdown (the §8 example). (Potential improvement)
- **Total rent earned** / **total rent paid** (also in 13.2/13.3). (Already covered in 13.2/13.3)
- **Maximum complete sets held at once** — `[snapshot]` max over the series of
  complete colour sets owned. [X]
- **Properties bought** / **won at auction** / **gained in deals** / **reserved** —
  `PropertyTransfer` by reason (`Buy` / `Auction` / `Deal` / `Reserved`, `Value > 0`). [X]
- **Properties lost** — handed into FP / returned to bank / given in deals / lost to
  bankruptcy — `PropertyTransfer` `Value < 0` by reason. [X]

### 13.5 Dice & movement
- **Total turn rolls** — `DiceRoll` (`IsTurnRoll`). [X]
- **Doubles rolled** / **triples rolled** — `DiceRoll.RollType`. [X]
- **Snake-eyes rolled** — `SneakEyes` count (or double of 1s). (irrelevant since we count how much we got from snake eyes in 13.3)
- **Times your dice-number came up** — `DiceNumBonus` events (split self vs others). [X]
- **Direction reversals** — `PlayerDirectionChanged`. [X]
- **Total distance travelled** — Σ board steps from `PlayerMoved`. [X]
- **Most-landed-on space** — `PlayerMoved.FinalBoardIndex` mode. [X]
- **Times landed on GO** / **Tax** / **Free Parking** — `PlayerMoved.FinalBoardIndex`
  == 0 / {4,38} / 20 (Tax cross-checks against `Tax`-reason receipt count). [X]

### 13.6 Jail
- **Times sent to jail** — `PlayerEnteredJailReceipt` `[needs receipt — currently
  unwired]`. [X]
- **Times left jail by paying** — `JailFee` events; **by double / by card** — split
  once card/jail receipts land. [X]
- **Total spent on jail** — `JailFee`. (covered in 13.2)
- **Turns spent in jail** — `[snapshot]` from `JailTurnCounter` progression. [X]

### 13.7 Free Parking
- **Times landed on FP** — `PlayerMoved` → 20. (Covered in Dice & Movement)
- **Total taken / times taken** — `FreeParkingTake`. (Covered in 13.3)
- **Total paid in / times paid** — `FreeParkingPay`. (Covered in 13.2)
- **Properties handed in / taken** — `PropertyTransfer` reason `FreeParking` (− / +). [X]
- **Sets handed into FP** — `[snapshot]` `FPHandedInSets`. [X]

### 13.8 Loans & mortgages
- **Loans taken** (count + total borrowed) — `LoanTake`. [X]
- **Loan repayments** (count + total repaid) — `LoanRepay`. [X] (repay amount covered in 13.2)
- **Outstanding loan debt at end** — `[snapshot]` `Loans`. [X]
- **Times mortgaged** (+ total raised) — `Mortgage`. [X]
- **Times unmortgaged** (+ total spent) — `Unmortgage`. [X]
- **Mortgage fees paid at GO** — `MortgageFee`. [X]

### 13.9 Cards `[needs receipts]`
- **Cards drawn** — `CardTaken` count.
- **Cards kept** — `CardTaken` where the keep flag is set.
- **Cards played (after keeping)** — `CardPlayed` count.
- **By card type** — breakdown over `CardType`.

### 13.10 Deals & player-to-player (DROP)
- **Deals completed** — count (from `Deal` receipts / `PropertyTransfer` reason `Deal`). 
- **Money / properties given vs received in deals** — `Deal` `Amount` ± and
  `PropertyTransfer` reason `Deal` ±. 

### 13.10 Endgame & outcome
- **Outcome** — `Winner` / `Drawn` / `Loser` (`GamePlayer.PlayerGameOutcome`). (Not needed; since this is on player and AppUser)
- **Did the player bankrupt** (+ voluntary?) — `PlayerBankrupted` (`VoluntaryBankruptcy`). [X]
- **Shortfall at bankruptcy** — `PlayerBankrupted.ShortfallAmount`. [X]
- **Bankruptcies benefited from** / **money from bankrupt players** — `BankruptedPlayer`. (Covered in 13.3)
- **Turns survived** — turn number at bankruptcy / game end `[snapshot]`. [X]
- **Final cash** / **final net worth** — `[snapshot]` last snapshot. [X]

### 13.11 State-over-time scalars `[snapshot]`
- **Peak net worth** (and the turn it occurred). *Net worth* = cash + Σ owned
  property value (mortgage value for mortgaged) + Σ building SELL value.** [X]
- **Peak cash.** (balance) [X]
- **Max complete sets held** (also 13.4).

### 13.12 Graph series — persisted as serialised arrays on `PlayerGameStat`
Computed once at conclusion so the graph screen reads only `PlayerGameStat`:
- **Money over time** (per turn). (balance) [X]
- **Net worth over time** (per turn). [X]
- **Property count over time** (per turn). [X]
- **Wealth rank over time** (net-worth rank among players, per turn). [X]

---

## 14. Open / TODO

1. **`PlayerEnteredJailReceipt` not emitted** — declared (and intended for the
   jail stats, §3.3) but not wired in the jail/movement path. Engine bug; fix so
   §13.6 jail stats are first-class. (Assumed available for stat design.)
2. **Cards deferred from the record** — the card stats (§13.9) are *not* in the
   initial `PlayerGameStat` (the cards subsystem is unbuilt). They slot in later:
   add the fields, bump `StatsVersion`, and the rebuild path (§12) backfills
   concluded games that have card receipts. No record-shape decision needed now.
3. **`event-receipts.md` amendment (deferred)** — §6/§9 there still say receipts
   are in-memory only; this doc adds the persisted derived log + materialised
   summary. Update that doc when this is built — **not yet**.
4. **`StatsVersion` rollout** — confirm the rebuild trigger granularity (rebuild
   all below current version vs targeted) when the recurring job is built.

**Resolved since the original draft:** final-turn flush (handled by
`TransitionToFinalTurn` persisting the last turn's events); caching policy
(materialise `PlayerGameStat`, §12); state-over-time path (persisted into
`PlayerGameStat`, §2/§13.12–13); persistence seam (`GameTurnEvents` via
`SnapshotService.CreateTurnEventSnapshotAsync`, built); **net-worth definition**
(pinned in §13.12 — cash + owned-property value, mortgage value if mortgaged, +
building *sell* value); cancelled games (contribute nothing, §11.C).

---

## 15. Traceability

1. **`event-receipts.md`** — the receipt taxonomy and the `FinancialTransactionReceipt`
   fields the projection reads; this doc is the persistence + projection layer it
   anticipated (pending the §14.3 amendment there).
2. **`game-engine.md` §8–§9** — the snapshot timeline this rides on, the
   start-of-turn snapshot timing (§6), and the `GameTurnStats` projection idea
   superseded for flow stats by the raw-events + materialised-summary model here.
3. **`turn-state.md`** — `TransitionToNextPlayer` / `TransitionToExtraTurn` /
   `TransitionToFinalTurn` are the boundaries where `ClearEvents` fires and the
   per-turn blob is captured (§5.3, §5.5).
4. **`game-rules.md`** — the rules behind the slices (rent, sets, fines, jail,
   loans) that give the stats meaning.
5. **Code** — built: `Models/DataModels/Games/GameTurnEvents.*`,
   `SnapshotService.CreateTurnEventSnapshotAsync`, the `GameCacheModel.Events` /
   `ClearEvents` seam. To build: `PlayerGameStat` model + migration,
   `GameStatsService.ComputeForGameAsync`, the `ConcludeGame` enqueue, the
   `MissingGameStatsJob` recurring safety-net, and the stat screens.
