# Turn State — Phase Gating & Transitions

The engine's coarse phase signal: where in a turn we are, what commands are
legal right now, and how phases flow into each other. Pairs with the prompt
framework (`choice-events.md`) — together they cover both sides of the
engine I/O loop. The prompt framework handles *engine pauses mid-execution*;
turn-state handles *what the player can initiate when the engine is idle*.

**Status:** design, foundational. The provider skeleton exists
(`Services/Framework/TurnStateProvider.cs`); transition methods are wired,
capability checks are wired, the player-advance step inside
`TransitionToNextPlayer` is a TODO. No turn-loop orchestration consumes it
yet — see §9.

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
is `StartOfTurn` again.

This contradicts `game-engine.md` §6 rule 1 ("the phase is part of the
serialised state, so an interrupted turn resumes mid-phase") which predates
the prompt-framework decision. That rule should be updated to reflect the
cache-only model.

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
              │             │   (same player, new snapshot,      │
              │             │    NO TurnNumber bump, NO advance) │
              │             │                                    │
              │             │── TransitionToNextPlayer ──────────┘
              └─────────────┘   (advance player, bump TurnNumber,
                                 new snapshot)
                ▲   both return GameModel for the caller to persist
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
- **`TransitionToExtraTurn(bool isTriple)`** — EndOfTurn → StartOfTurn for
  the *same* player. Commits the working state, clears the per-turn event
  window, bumps the matching `DoublesInRow` / `TriplesInRow` counter
  (and resets the other per `game-rules.md` Doubles/Triples rule 6), and
  returns the resulting `GameModel` for the caller to persist as the
  extra turn's snapshot. **Does not bump `TurnNumber` or change
  `CurrentPlayerId`** — that's `TransitionToNextPlayer`'s job.
- **`TransitionToNextPlayer()`** — EndOfTurn → StartOfTurn for the next
  player. Commits the working game state, clears the per-turn event list,
  advances the player, bumps `TurnNumber`, and **returns the resulting
  `GameModel`** for the caller to persist and broadcast.

### "Extra turn" vs "next player"

Both fire from EndOfTurn, both commit, both clear events, both return a
`GameModel` for persistence. The differences are:

| | `TransitionToExtraTurn` | `TransitionToNextPlayer` |
|---|---|---|
| `CurrentPlayerId` | unchanged | advances to next player |
| `TurnNumber` | unchanged | bumps |
| `CurrentTurnId` (GameTurn) | **unchanged** — snapshot sits under the same GameTurn record | new GameTurn record created |
| New GameSnapshot row | yes (under existing GameTurn) | yes (under the new GameTurn) |
| `DoublesInRow` / `TriplesInRow` | bumped per `isTriple`, the other resets | unchanged |
| Result for the player | rolls again as the same player | their turn is over |

The key model: **`GameTurn` is the "whose turn is it" record** — created
exactly once per player-advance, holding `CurrentTurnId`, `TurnNumber`,
`CurrentPlayerId`. **`GameSnapshot` is the per-state snapshot** — one row
per commit (per turn boundary), multiple rows can sit under a single
`GameTurn` when extra turns fire. So an extra turn is a *new snapshot* but
the *same GameTurn*; a next-player advance is both a *new snapshot* and a
*new GameTurn*.

This matches how players experience it — "you go again" feels like a new
turn (full portfolio window, deal opportunity, fresh roll, fresh
snapshot), but the turn pacing and turn count don't advance.

### Why does the EndOfTurn idle window matter for extra turns?

The deal opportunity. Without routing extra turns through EndOfTurn, the
current player would roll, get a double, and immediately roll again — no
chance for anyone to propose a deal to them in between. By gating
`TransitionToExtraTurn` on EndOfTurn, the deal-window applies between
every roll, not just between players. Same for voluntary bankruptcy.

---

## 5. Snapshot Bubbling

Both `TransitionToNextPlayer()` and `TransitionToExtraTurn()` return a
`GameModel`. The engine knows nothing about storage (per
`game-engine.md` §3), so the snapshot has to bubble up out of the engine
to the web layer, which serialises and persists it. The return type is
the engine's own `GameModel` (a plain POCO) — no JSON, no EF — so no
storage concern leaks into the engine.

Higher-level orchestration (turn-loop / hub method) is then responsible
for:

1. Receiving the returned `GameModel`.
2. Serialising and persisting it to the snapshot store.
3. Broadcasting via SignalR if needed.

The provider itself does none of this. Each transition produces a new
`GameSnapshot` row; only `TransitionToNextPlayer` produces a new
`GameTurn` row. An extra-turn snapshot sits *under the existing
GameTurn* — same `CurrentTurnId`, same `TurnNumber`, same
`CurrentPlayerId` — but is its own snapshot row with its own per-turn
event window.

This means the GameTurn↔GameSnapshot relationship is **1:many**, not 1:1
as `game-engine.md` §8 currently states (the doc claims "shared primary
key" — should be a FK from GameSnapshot to GameTurn instead). See §9
TODO.

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
| Game state (players, properties, money, etc.) | `GameCacheModel.Game` (`GameModel`) | **yes** (committed via `SaveChanges` at turn boundary, snapshot per turn) | rule code, persisted at `TransitionToNextPlayer` |
| `TurnDiceRoll` (current roll) | `GameCacheModel` | no | `SetTurnDiceRoll` |

`SaveChanges` fires at every turn boundary that produces a snapshot —
both `TransitionToNextPlayer` *and* `TransitionToExtraTurn`. Each commit
yields a `GameModel` for the caller to persist as a new `GameSnapshot`
row. `TurnNumber`, `CurrentPlayerId`, and `CurrentTurnId` all advance only
on `TransitionToNextPlayer` (which also creates the new `GameTurn`
record); `TransitionToExtraTurn` leaves all three unchanged — the new
snapshot is unique by its own primary key, not by anything in the
metadata.

---

## 8. Cross-References

- **`choice-events.md` §2** — the commands vs prompts split. Turn-state
  gates commands; it is silent on prompts.
- **`game-engine.md` §6** — describes the same phase machine in narrative
  form. Note rule 1 conflict (§1 above) — needs updating.
- **`game-rules.md`** — the rule references underlying each capability
  gate (Bankruptcy rule 1 for voluntary bankruptcy timing; Doubles /
  Triples rules for extra-turn branches; Default rule 7 for the sell-only
  restriction on buy/bid).

---

## 9. Open / TODO

Tracked here until resolved.

1. **`AdvancePlayer` is a stub.** `TransitionToNextPlayer` mutates the
   state and returns the snapshot, but the actual "find the next eligible
   player" logic is unimplemented. Real implementation needs to:
   - Skip bankrupt players (`game-rules.md` Bankruptcy).
   - Decrement `TurnsToMiss` and skip missed-turn players (Double 2
     effect).
   - Generate a new `CurrentTurnId`.
   - Bump `TurnNumber`.

   Borders on game logic, so deliberately kept out of the foundational
   skeleton.

2. **`game-engine.md` §8 has the GameTurn↔GameSnapshot relationship
   wrong.** It claims "1:1 with shared primary key". The correct model
   (per the extra-turn distinction in §4 above) is 1:many — multiple
   GameSnapshot rows can sit under a single GameTurn when extra turns
   fire. GameSnapshot needs its own PK with an FK to GameTurn. The doc
   should be updated.

3. **Roll-phase transition is duplicated.** `TransitionToRollPhase()` on
   the provider and `cache.SetTurnDiceRoll(...)` both move StartOfTurn →
   PlayerRollMovement. The latter does it implicitly when the roll lands;
   the former is a manual entrypoint. Either:
   - Drop `TransitionToRollPhase` and let `SetTurnDiceRoll` be the only
     trigger (one path, less surface).
   - Have `SetTurnDiceRoll` call into the provider instead of
     `SetTurnState` directly (provider remains sole transitioner).

   Worth deciding once the turn-loop orchestration shape is clearer.

4. **`game-engine.md` §6 rule 1 contradicts the cache-only model.** That
   doc says phase is in the serialised state; the prompt-framework restart
   contract and this design say phase is cache-only. The doc should be
   updated.

5. **Counter management is a small bit of game logic in a framework
   method.** `TransitionToExtraTurn` bumps `DoublesInRow` / `TriplesInRow`
   and resets the other counter (Doubles/Triples rule 6). That's
   technically game logic, but the bump is intrinsic to the transition
   (the transition only fires because a double/triple granted the extra
   turn). The "right" place for the counter is debatable — could move to
   engine code that triggers the transition, leaving the transition pure
   state-machine. Flagged so the boundary is explicit.

6. **Bilateral-deal gate.** `CanDeal(playerId)` only checks the calling
   player is at their own turn boundary. A real deal needs *both*
   parties to be reachable — the other party must also be at a boundary.
   Engine layer responsibility for now; if the pattern repeats elsewhere,
   could factor into a `CanDealBetween(playerA, playerB)`.

7. **No tests yet.** Each `Can…` method and each transition needs unit
   tests covering the allowed/disallowed cases and the wrong-state
   throws.

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
   skeleton implementation.