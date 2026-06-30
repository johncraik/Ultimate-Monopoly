# Turn State — Phase Gating & Transitions

The engine's coarse phase signal: where in a turn we are, what commands are
legal right now, and how phases flow into each other. Pairs with the prompt
framework (`choice-events.md`) — together they cover both sides of the
engine I/O loop. The prompt framework handles *engine pauses mid-execution*;
turn-state handles *what the player can initiate when the engine is idle*.

**Status:** built and in use. The provider lives in
`Services/Framework/TurnStateProvider.cs` (capability checks, transitions,
snapshot writes via `ISnapshotService`, and player advancement) and is driven
by `PlayerTurnOrchestrator`. Some progress notes below were written before that
wiring landed — see the drift note at the foot of this document.

---

## 1. What `TurnState` Is

`TurnState` is a coarse, four-valued enum on `GameCacheModel` describing the
phase of the current player's turn:

```csharp
enum TurnState { StartOfTurn, PlayerRollMovement, ThirdDieMovement, EndOfTurn }
```

1. **`StartOfTurn`** — the pre-roll idle window. Player can manage their
   portfolio, propose deals, declare bankruptcy, choose how to leave jail.
   The engine is not running anything; it's waiting for the player to act.
2. **`PlayerRollMovement`** — the engine is mid-execution resolving the
   roller's roll: doubles/triples effects, roller movement, landed-space
   actions.
3. **`ThirdDieMovement`** — the engine is mid-execution moving every
   other player on the third die and resolving their landed-space actions.
4. **`EndOfTurn`** — the post-movement idle window. Player can propose
   deals, declare bankruptcy, or end the turn. Portfolio commands are
   **not** legal here.

`StartOfTurn` and `EndOfTurn` are *idle* windows; `PlayerRollMovement` and
`ThirdDieMovement` are *execution* windows. The cache field
`PendingPrompt` is the orthogonal "engine is paused waiting on input" flag
— both must be checked together for a true "engine is idle" gate (see §3).

### Lives on the cache, not the snapshot

`TurnState` is on `GameCacheModel`, not inside `GameModel`. This is the
correct shape per the prompt-framework restart contract: a turn interrupted
by a restart is lost, and the player re-rolls from the pre-turn snapshot.
There is therefore no need to persist mid-turn phase — the snapshot is
always taken at `StartOfTurn`, and the next thing that runs after a restart
is `StartOfTurn` again. `game-engine.md` §6 rule 1 reflects this.

---

## 2. The Strict Boundary: Commands vs Prompts

The framework split established in `choice-events.md` §2 carries through
here. Turn-state gates **commands** (player-initiated actions when the
engine is idle); it does *not* gate prompts (engine-initiated pauses
mid-execution).

| Direction | Mechanism | Gated by |
|---|---|---|
| Player → engine (idle) | Command | `TurnStateProvider.CanXyz(playerId)` |
| Engine → player (mid-exec) | Prompt | the engine itself — gated only by validator (auth + payload shape) |

A command rejected by the provider should never mutate state — the hub /
service short-circuits before calling the engine. A prompt that opens
during execution is the engine's decision; turn-state doesn't intervene.

---

## 3. Capability Gates

The provider exposes one `Can…` method per command group. Each composes
small private primitives — there is no boolean spaghetti in callers.

### Private primitives

- **`IsEngineIdle()`** — `cache.PendingPrompt is null`. False when any
  prompt is awaiting a response.
- **`IsCurrentPlayer(playerId)`** — the named player is whose turn it
  currently is.
- **`IsJailed(playerId)`** — the named player is in jail right now.
- **`IsAtTurnBoundary()`** — `TurnState` is `StartOfTurn` or `EndOfTurn`.

### Command groups and their gates

| Command group | Gate |
|---|---|
| **Portfolio** (mortgage/unmortgage, build, sell houses, play card from hand, pay loan early) | `StartOfTurn` only, current player, not in jail, engine idle |
| **Deal** (propose / accept) | At turn boundary (Start or End), engine idle. Bilateral check (the *other* party must also be reachable) is engine-layer |
| **Jail exit** (pay fee / play card / attempt double) | `StartOfTurn`, current player, in jail, engine idle |
| **End turn** | `EndOfTurn`, current player, engine idle |
| **Voluntary bankruptcy** | At turn boundary, engine idle (any player) |

### "Why is portfolio start-only?"

Portfolio actions exist in the idle window *before* the player commits to
rolling. Once they've rolled and resolved movement, there is no further
opportunity to mortgage/build/etc. before the turn ends — the only
legitimate end-of-turn actions are settling deals and ending the turn.

### "Why is voluntary bankruptcy not literally any time?"

`game-rules.md` Bankruptcy rule 1 says "at any time" but the intended
reading is "at any of their turn boundaries", not "literally any moment in
the middle of another player's execution". Voluntary bankruptcy initiated
*as a shortfall response* is a separate path: `ShortfallPrompt` carries it
as a response action, so the player can still surrender when the engine
has them mid-pay.

### Sell-houses: two routes

Selling buildings is allowed both as a portfolio command (turn-idle, free
choice) and as a `ShortfallPrompt` response (mid-execution, to cover a
debt). Both routes are legal. Default rule 7 only restricts using either
route to fund a *new* commitment (buying or bidding) — that's enforced at
the buy/bid path, not at the sell path.

---

## 4. Transitions

The provider owns every legal change of `TurnState`. Outside code calls
into the named methods; it never sets `TurnState` directly. Each
transition validates the current state before mutating and throws when
called from the wrong place — bugs surface loudly rather than corrupting
state silently.

```
              ┌─────────────┐
              │ StartOfTurn │◀───────────────────────────────────┐
              └─────────────┘                                    │
                     │                                           │
                     │ TransitionToRollPhase                     │
                     ▼                                           │
              ┌─────────────────────┐                            │
              │ PlayerRollMovement  │                            │
              └─────────────────────┘                            │
                     │      │                                    │
   TransitionToThirdDie    TransitionToEndOfTurn                 │
                     │      │   (triple — skips ThirdDie         │
                     │      │    per Triples rule 3)             │
                     ▼      │                                    │
              ┌─────────────────────┐                            │
              │  ThirdDieMovement   │                            │
              └─────────────────────┘                            │
                     │                                           │
                     │ TransitionToEndOfTurn                     │
                     ▼                                           │
              ┌─────────────┐                                    │
              │  EndOfTurn  │── TransitionToExtraTurn(isTriple) ─┤
              │             │   (same player, bump TurnNumber,   │
              │             │    new GameTurn + GameSnapshot)    │
              │             │                                    │
              │             │── TransitionToNextPlayer ──────────┘
              └─────────────┘   (advance player, bump TurnNumber,
                                 new GameTurn + GameSnapshot)
                ▲   both write a snapshot via ISnapshotService
                │   (caller-driven tx supported via overload)
                │
            EndOfTurn is the deal/bankruptcy idle window — the player
            settles before either ending or taking their extra turn.
```

### The methods

- **`TransitionToRollPhase()`** — StartOfTurn → PlayerRollMovement. Player
  has finished portfolio and is rolling.
- **`TransitionToThirdDie()`** — PlayerRollMovement → ThirdDieMovement.
  Normal roll or non-triple double; other players take the third die.
- **`TransitionToEndOfTurn()`** — to EndOfTurn. Allowed from
  PlayerRollMovement (triple — skips ThirdDie per Triples rule 3; or
  3-in-a-row sending the roller to jail) or ThirdDieMovement (normal end
  — other players have moved).
- **`TransitionToExtraTurn(bool isTriple)`** — `async Task`. EndOfTurn →
  StartOfTurn for the *same* player. Commits the working state, clears
  the per-turn event window, bumps the matching `DoublesInRow` /
  `TriplesInRow` counter (and resets the other per `game-rules.md`
  Doubles/Triples rule 6), advances turn metadata (new `CurrentTurnId`,
  `TurnNumber++`; `CurrentPlayerId` unchanged), and writes a snapshot
  via `ISnapshotService.CreateSnapshotAsync` — a new `GameTurn` row plus
  its `GameSnapshot`. **`CurrentPlayerId` is unchanged**; everything else
  about the turn advances.
- **`TransitionToNextPlayer()`** — `async Task`. EndOfTurn → StartOfTurn
  for the next player. Commits the working game state, clears the per-
  turn event list, advances the player (`AdvancePlayer` — see §9),
  bumps `TurnNumber`, generates a new `CurrentTurnId`, and writes a
  snapshot via `ISnapshotService.CreateSnapshotAsync` — a new `GameTurn`
  row plus its `GameSnapshot` for the turn just beginning.

### "Extra turn" vs "next player"

Both fire from EndOfTurn, both commit, both clear events, both write a
snapshot via `ISnapshotService`. The differences are:

| | `TransitionToExtraTurn` | `TransitionToNextPlayer` |
|---|---|---|
| `CurrentPlayerId` | **unchanged** | advances to next player |
| `TurnNumber` | bumps | bumps |
| `CurrentTurnId` | new (regenerated in `UpdateMetadata`) | new (regenerated in `UpdateMetadata`) |
| New `GameTurn` + `GameSnapshot` row | yes | yes |
| `DoublesInRow` / `TriplesInRow` | bumped per `isTriple`, the other resets | unchanged |
| Result for the player | rolls again as the same player | their turn is over |

The only thing distinguishing an extra-turn `GameTurn` row from a
next-player `GameTurn` row at the schema level is that consecutive rows
share `CurrentPlayerId`. Everything else — new id, bumped TurnNumber,
new snapshot, cleared events — is identical.

The key model: **`GameTurn` is the "one snapshot's worth of turn"
record**, 1:1 with `GameSnapshot` and identified by `CurrentTurnId`. A
*player's* turn is a sequence of `GameTurn` rows sharing
`CurrentPlayerId` (one row per roll-and-resolve cycle the player goes
through). This matches how players experience it — "you go again" feels
like a new turn (full portfolio window, deal opportunity, fresh roll,
fresh snapshot) — and matches the schema cleanly without needing
1:many.

### Why does the EndOfTurn idle window matter for extra turns?

The deal opportunity. Without routing extra turns through EndOfTurn, the
current player would roll, get a double, and immediately roll again — no
chance for anyone to propose a deal to them in between. By gating
`TransitionToExtraTurn` on EndOfTurn, the deal-window applies between
every roll, not just between players. Same for voluntary bankruptcy.

---

## 5. Snapshot Persistence — Through an Abstraction

Both `TransitionToNextPlayer()` and `TransitionToExtraTurn()` write a
snapshot by calling `ISnapshotService.CreateSnapshotAsync(cache.Game)`.
The engine still knows no storage *mechanism* — `ISnapshotService` is
declared in `MP.GameEngine.Abstractions` and implemented in the web
project, so the engine only knows "take a snapshot of this game", not
whether the implementation writes DB rows, files, or anything else. See
`game-engine.md` §3 rule 1.

The contract:

1. **Producer mutates the model in place.** The snapshot service
   generates the new `GameTurn.Id` and writes it back to
   `game.Metadata.CurrentTurnId` on the passed `GameModel`. The
   transition then calls `cache.SaveChanges()` to promote that mutation
   into the committed cache state. This is the *only* way the engine
   learns the just-persisted turn id.
2. **Engine throws on persistence failure.** `CreateSnapshotAsync`
   reports failure via exception only; there is no bool return. The
   transition propagates — the cache state stays uncommitted.
3. **Caller-driven transactions are supported.** The interface takes a
   `bool completeTransaction = true` parameter; passing `false` lets the
   caller compose the snapshot insert into a larger outer transaction
   (used by `TryStartGame` to combine the initial snapshot with the
   `Game.State` flip in one tx). The default `true` is right for the
   transitions, which have no outer tx.
4. **Broadcasting is still the caller's job.** The provider doesn't know
   SignalR. The orchestration layer (turn-loop / hub) reads post-call
   state from `cache.Game` and broadcasts as appropriate.

Each transition produces one new `GameTurn` row + one new `GameSnapshot`
row — the 1:1 relationship `game-engine.md` §8 describes is preserved.
Extra-turn rows are distinguished from next-player rows only by sharing
`CurrentPlayerId` with their predecessor (see §4 table).

---

## 6. Stateful Helper, Not Driver

The provider is **stateful** (it holds a `GameCacheModel`) but it is not
the **driver** of the turn loop. Higher-level code — the turn-execution
loop that opens prompts, processes commands, runs the rules engine —
decides *when* to call the transitions. The provider only owns:

- The rules of what's allowed (capability gates).
- The rules of what the next state is (transitions and their preconditions).

This mirrors the prompt framework: `IPromptProvider` is the seam the
engine uses to emit prompts, but the *what to prompt about* lives in the
engine code that calls it. Same here: `TurnStateProvider` is the seam the
engine uses to gate and transition, but the *when to transition* lives in
the engine code that calls it.

---

## 7. State Lives Where?

| Concern | Lives in | Survives restart? | Who mutates |
|---|---|---|---|
| `TurnState` | `GameCacheModel` | no | `TurnStateProvider` only (via internal `SetTurnState`) |
| `PendingPrompt` | `GameCacheModel` | no | `PromptProvider` (via `SetPendingPrompt` / `ClearPendingPrompt`) |
| Events for this turn | `GameCacheModel` | no | engine code (via `AddEvent` / `ClearEvents`) |
| Game state (players, properties, money, etc.) | `GameCacheModel.Game` (`GameModel`) | **yes** (committed via `SaveChanges` at turn boundary; snapshot per turn via `ISnapshotService`) | rule code, persisted at both `TransitionToNextPlayer` *and* `TransitionToExtraTurn` |
| `TurnDiceRoll` (current roll) | `GameCacheModel` | no | `SetTurnDiceRoll` |

`SaveChanges` fires at every turn boundary that produces a snapshot —
both `TransitionToNextPlayer` *and* `TransitionToExtraTurn` — and each
transition calls `ISnapshotService.CreateSnapshotAsync` to persist. Both
also bump `TurnNumber` and regenerate `CurrentTurnId` (via the shared
`UpdateMetadata` helper); they differ only in whether `CurrentPlayerId`
advances (next-player) or stays the same (extra-turn).

---

## 8. Cross-References

- **`choice-events.md` §2** — the commands vs prompts split. Turn-state
  gates commands; it is silent on prompts.
- **`game-engine.md` §6** — describes the same phase machine in narrative
   form; rule 1 there now aligns with the cache-only model (§1 above).
- **`game-engine.md` §3 + §8** — the engine's storage-contract layering
  (engine knows the contract, not the mechanism) and the GameTurn /
  GameSnapshot 1:1 schema.
- **`game-rules.md`** — the rule references underlying each capability
  gate (Bankruptcy rule 1 for voluntary bankruptcy timing; Doubles /
  Triples rules for extra-turn branches; Default rule 7 for the sell-only
  restriction on buy/bid).
- **`MP.GameEngine/Abstractions/ISnapshotService.cs`** — the persistence
  contract both transitions invoke.

---

## 9. Open / TODO

Tracked here until resolved.

1. **`AdvancePlayer` is a basic stub.** The current implementation does
   next-OrderId-with-wraparound — enough to exercise the framework with
   non-bankrupt, non-missing players. Two `game-rules.md` cases aren't
   handled yet:
   - Skip bankrupt players (Bankruptcy rule).
   - Decrement `TurnsToMiss` and skip missed-turn players (Double 2
     effect).

   These will likely extract into a dedicated helper class above the
   provider when the turn-loop orchestration shape is clearer. The
   helper should decide which transition fires (extra-turn vs next-
   player) and own these harder cases — keeping the provider a pure
   state-machine and pulling the increasingly-game-logic bits out. See
   the TODO comment on `TurnStateProvider.AdvancePlayer`.

2. **Roll-phase transition is duplicated.** `TransitionToRollPhase()` on
   the provider and `cache.SetTurnDiceRoll(...)` both move StartOfTurn →
   PlayerRollMovement. The latter does it implicitly when the roll lands;
   the former is a manual entrypoint. Either:
   - Drop `TransitionToRollPhase` and let `SetTurnDiceRoll` be the only
     trigger (one path, less surface).
   - Have `SetTurnDiceRoll` call into the provider instead of
     `SetTurnState` directly (provider remains sole transitioner).

   Worth deciding once the turn-loop orchestration shape is clearer
   (likely settled by `DiceService` — see `game-engine.md` §13 build
   order).

3. **Counter management is a small bit of game logic in a framework
   method.** `TransitionToExtraTurn` bumps `DoublesInRow` / `TriplesInRow`
   and resets the other counter (Doubles/Triples rule 6). That's
   technically game logic, but the bump is intrinsic to the transition
   (the transition only fires because a double/triple granted the extra
   turn). The "right" place for the counter is debatable — could move to
   the future advancement-helper (#1) that decides which transition to
   fire, leaving the transition pure state-machine. Flagged so the
   boundary is explicit.

4. **Bilateral-deal gate.** `CanDeal(playerId)` only checks the calling
   player is at their own turn boundary. A real deal needs *both*
   parties to be reachable — the other party must also be at a boundary.
   Engine layer responsibility for now; if the pattern repeats elsewhere,
   could factor into a `CanDealBetween(playerA, playerB)`.

### Resolved (kept here for the record)

- ~~`game-engine.md` §6 rule 1 contradicts the cache-only model.~~
  Updated 2026-05-25 — rule 1 now states phase is cache-only.
- ~~`game-engine.md` §8 GameTurn↔GameSnapshot is 1:many.~~ Resolved by
  design pivot 2026-05-25 — extra turns now mint their own GameTurn row,
  so the 1:1 schema stands (see §4). No migration needed.
- ~~No tests yet.~~ 106 tests in
  `MP.GameEngine.Tests/FrameworkTests/TurnStateProvider_Tests.cs` cover
  every `Can…` method and every transition (allowed / disallowed /
  wrong-state-throws / snapshot-service invocation).

---

## 10. Traceability

1. **`game-rules.md`** — the rules whose timing the gates enforce.
2. **`game-engine.md`** — the surrounding engine architecture and the
   narrative phase machine (§6).
3. **`choice-events.md`** — the prompt framework; the commands-vs-prompts
   boundary (§2) is the contract this doc consumes.
4. **`turn-state.md`** (this doc) — the gating and transition semantics
   commands use.
5. **`MP.GameEngine/Services/Framework/TurnStateProvider.cs`** — the
   implementation.

---

## Implementation status & drift

> This document records the **agreed design**, not the live state of the code.
> Since it was written the implementation has moved on — much of what is
> described here is built, and some details have changed. Any status, "TODO",
> "not yet built", or "pre-implementation" note above may be out of date.
>
> Where this doc and the code disagree, the **code (and the developer) win**
> (`docs/development/README.md`). Verify specifics against the current code.