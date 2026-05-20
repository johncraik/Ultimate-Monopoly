# Game Engine — Architecture & Design

The architectural counterpart to `game-rules.md`. That document defines *what*
the game does; this one defines *how* the application implements it. Decisions
recorded here are the agreed design — it is an authoritative document, not a
sketch.

**Status:** design, pre-implementation. No game engine exists yet — the app is
currently a configuration and social platform (board skins, friends, profiles).
This doc is the plan for building the engine on top of it.

---

## 1. Purpose & Scope

The application is a **helper**, not a digital simulator.

1. **Players play on a physical board.** Real dice are rolled, real tokens and
   houses are moved. The app does not replace the board.

2. **The app is the authoritative ledger.** It owns everything fiddly and
   error-prone: money, loans, mortgages, card hands and decks, property
   ownership and buildings, the Free Parking pot, and all the bespoke
   bookkeeping (dice numbers, doubles/triples counters, the triple-bonus
   accumulator, jail counters, per-set hand-in tracking, reserved properties).

3. **The app tracks board positions.** It must, in order to be a helper: the
   three-dice mechanic moves every player on each roll, and the app computes
   those movements and their consequences from a single roll input.

4. **The board layout is already known.** The existing board-skin feature
   defines spaces, colours, and prices — the engine consumes that.

5. **Dice are input, not generated.** Players roll real dice and enter the
   result. A virtual dice option may be added later (see §10) but is out of
   scope for the first build.

**In scope:** the rules engine, turn loop, the custom economy systems,
persistence, and the card system. **Out of scope for now:** a digital board
UI, AI opponents, and per-deck card *contents* (a separate doc — see §11).

---

## 2. Guiding Decisions

The non-negotiable decisions the rest of the design follows from.

1. **Helper, not simulator.** See §1.

2. **The rules engine is a separate, pure library.** No EF, no web, no `JC.*`
   web dependencies. This makes it unit-testable, which is mandatory given the
   rule complexity. See §3.

3. **Live game state and persistence entities are different things.** The
   engine owns a rich in-memory state model; the database stores serialised
   snapshots of it. See §5 and §8.

4. **Every turn is snapshotted, and snapshots are kept.** A game is a timeline
   of per-turn snapshots, not a single mutable record. This one mechanism
   serves persistence, crash recovery, the game cache, backtrack, replay, and
   analytics. See §8 and §9.

5. **Cards are a fixed, predefined set.** Players cannot author their own
   cards. This collapses the card system from an open-ended effect interpreter
   into a closed set of hand-written, individually testable handlers — and is
   the key reason this attempt is tractable where previous ones failed. See
   §11.

6. **`game-rules.md` is the contract.** The doc, the engine, and the tests move
   together. Every rule change is a doc change, a test change, and an engine
   change in lockstep.

---

## 3. Solution Structure

Today the solution is a single project — `UltimateMonopoly` (ASP.NET Core 9
Razor Pages). The engine is added as new class-library projects.

| Project | Role | Depends on |
|---|---|---|
| `UltimateMonopoly.GameEngine` | Pure rules engine + live-state model. No EF, no web. | nothing (BCL only) |
| `UltimateMonopoly.GameEngine.Tests` | Unit tests for the engine. | GameEngine |
| `UltimateMonopoly` (existing web app) | Razor Pages UI, EF persistence, snapshot storage, wiring. | GameEngine |

1. **The engine knows nothing about storage or the web.** It exposes a game
   state object, accepts commands, and produces new state plus events. The web
   project is responsible for loading/saving snapshots and rendering.

2. **The web project maps between worlds.** It loads the latest snapshot,
   deserialises it into engine state, applies commands, then serialises the
   result back. EF entities and the engine state never reference each other's
   types directly.

3. **The board comes from the existing board-skin model.** The engine takes a
   resolved board definition (spaces, colours, prices) as input — it does not
   own board-skin persistence.

---

## 4. The Rules Engine

A deterministic, command-driven core.

1. **Commands in, events + state out.** Every player action is a *command*
   (`RollDice`, `BuyProperty`, `DeclinePurchase`, `TakeLoan`, `PlayCard`,
   `BuildHouse`, `DeclareBankruptcy`, …). The engine validates a command
   against the current state and the rules, then produces the next state plus
   a list of *events* describing what happened.

2. **Events drive the UI and the stats projection.** Events (`PlayerMoved`,
   `RentPaid`, `FineToFreeParking`, `LoanTaken`, `CardDrawn`, …) are the
   engine's narration of a turn. The web layer renders them; the stats
   projection (§9) reads them.

3. **Validation is the engine's job, not the UI's.** "Can this player buy?",
   "is this loan permitted?", "has GO been passed?" — all answered inside the
   engine. The UI only offers actions; it never enforces rules.

4. **Determinism.** Given a state and a command (including the dice values in
   the command), the resulting state is fully determined. No hidden randomness,
   no wall-clock dependence — this is what makes replay and backtrack correct
   (see §9).

5. **The rules engine is deliberately testable.** `game-rules.md` is numbered
   and atomic precisely so each rule maps to test cases. Test names reference
   their rule, e.g. `DoubleRolls_Rule5_ThreeInARowSendsToJail`. The doc is the
   test plan.

---

## 5. Game State Model

Two distinct representations, deliberately kept apart.

### Live game state — owned by the engine

The rich object the engine mutates. Pure POCOs in `UltimateMonopoly.GameEngine`,
no EF attributes, fully serialisable. Holds, among other things:

- Each player's position, **direction of travel**, money, dice number, loans,
  owned/mortgaged/reserved properties, buildings, card hand, jail state and
  turn counter, triple-bonus accumulator, doubles/triples-in-a-row counters.
- Deck state for every card deck (draw order, discards).
- The Free Parking pot and any properties held in it.
- Per-set hand-in tracking.
- The current turn and **turn phase** (see §6).

### Persistence entities — owned by the web/data layer

EF entities for cross-game queries and storage. They do *not* hold the live
state field-by-field — see §8. They cover game **metadata**: `Game` (id, name,
`State`, `Outcome`, `RoundingRule`, `BoardId`, timestamps), the player roster,
and the snapshot tables.

1. **`GamePlayer` is being repurposed.** Today's `GamePlayer` entity holds live
   state (position, money, dice, jail cost, triple bonus) as EF columns. Under
   this design those mutable fields belong in the snapshot; the `GamePlayer`
   row shrinks toward identity + a queryable summary. Exact split is an open
   decision (§14).

2. **The engine never sees an EF type, and EF never sees an engine type.** The
   web layer serialises one into the other.

---

## 6. The Turn — Phase State Machine

A turn is a multi-phase sequence, not a single action. Model it as an explicit
state machine, not nested conditionals.

Outline of the phases (to be refined against `game-rules.md` during build):

1. **Start of turn** — **write the snapshot** of the incoming state (§8), then
   resolve missed-turn / jail status.
2. **Roll** — the player enters three dice (two main + third).
3. **Special-roll resolution** — detect double / triple, apply the relevant
   section's effects, collect the relevant card.
4. **Move** — move the rolling player; apply the landed space's action.
5. **Third-die movement** — move every other player in clockwise turn order,
   each in their own facing direction; apply each landed space's action.
6. **Dice-number payouts** — resolve any player whose dice number was rolled.
7. **Card resolution** — collected cards drawn/resolved; interrupts (NOPE)
   handled.
8. **Extra roll?** — doubles/triples grant another roll → back to phase 2.
9. **End of turn** — advance to the next player. The next player's turn opens
   by snapshotting (phase 1), so the snapshot always captures pre-turn state.

1. **Phases gate which commands are legal.** The engine rejects a `BuyProperty`
   command outside the phase where a purchase is offered. The phase is part of
   the serialised state, so an interrupted turn resumes mid-phase.

2. **Sub-turn events are plentiful.** Forward-and-back double movement, and the
   third-die movement of every player, each trigger space actions — a single
   turn can produce many events and sub-decisions.

---

## 7. Commands & Events

1. **A command is a serialisable record of a player's intent.** It carries
   everything the engine needs, including dice values — the engine adds no
   randomness of its own.

2. **The engine returns `(newState, events[])` or a validation failure.** No
   partial mutation: a rejected command leaves state untouched.

3. **Events are append-only narration.** They are not persisted as the source
   of truth (the snapshot is — §8), but they may be logged within a turn for
   fine-grained recovery and they feed the UI and the stats projection.

---

## 8. Persistence — Snapshots

The full game state is persisted to the database **every turn**, as a
serialised document — not normalised into relational tables.

1. **Why a document, not a table tree.** The live state is deeply nested with
   transient counters, card hands, and decks. Exploding it into
   `GamePlayerCards`, `GamePropertyOwnership`, `GameDeckOrder`, `GameLoans` …
   tables means a painful EF mapping and a migration every time the rules
   change — and the engine gains nothing, because it works on the in-memory
   object, not SQL rows.

2. **Storage shape — two tables.** `GameTurn` holds per-turn metadata (turn
   number, the player, `IsFinalTurn`, audit + soft-delete); `GameSnapshot` is
   1:1 with it (shared primary key) and holds the serialised `StateJson` (JSON
   / `LONGTEXT`). Conceptually "a folder per game, one file per turn" — but as
   DB rows, so it is transactional with the rest of the data, survives
   restarts, and needs no filesystem handling. Splitting the metadata from the
   blob keeps turn listing and ordering cheap without touching the JSON.

3. **A thin relational header stays queryable.** The `Game` entity keeps the
   columns needed for cross-game queries — `State`, `Outcome`, `BoardId`,
   player roster, current turn number, timestamps — so "my active games",
   "completed games", and the load-game list are plain SQL, no blob parsing.

4. **This satisfies all three persistence requirements:**
   - **Per-turn saving** — a snapshot is written at the *start* of each turn,
     capturing the state the turn begins from (§6). The first turn's snapshot
     is therefore the initial game state.
   - **Load later** — read the latest row, deserialise, hand to the engine.
   - **Crash / restart survival** — identical path; worst case the in-progress
     turn is lost and re-entered. If finer recovery is wanted, an intra-turn
     event log can be layered on, but per-turn snapshot is the baseline.

5. **The "game cache" is just the latest snapshot in memory.** John's planned
   game cache and this persistence model are the same mechanism: the cache is
   the deserialised latest snapshot; it is written back each turn.

6. **Snapshots must be complete and deterministic.** A snapshot has to
   reconstruct the engine state with **zero hidden in-memory state** — deck
   order, card hands, every counter, and (once virtual dice exist) the RNG
   state or the recorded rolls. The live-state POCO is designed to be fully
   self-contained and serialisable for this reason.

---

## 9. Snapshot History — Backtrack, Replay, Stats

Because snapshots are *kept*, not overwritten, a game is a timeline. Storage
cost is negligible (a state JSON is a few KB–tens of KB; a game is dozens to
low-hundreds of turns → single-digit MB per game).

1. **Backtrack.** Loading turn *K*'s snapshot rewinds the game to that turn.
   This is high-value in a helper app, where backtrack means "we miscounted —
   undo".

2. **Backtrack semantics — truncate with soft-delete.** Rewinding to turn *K*
   discards turns *K+1…N*; those snapshot rows are **soft-deleted**, not
   hard-deleted, preserving the audit trail. (Branching into alternate
   timelines was considered and rejected as overkill.) Note the human side: the
   app rewinds, but players must physically un-move tokens.

3. **Replay.** The same snapshot series can be stepped through to review or
   spectate a game.

4. **Stats projection.** Alongside each snapshot, write a small flat
   `GameTurnStats` row per player per turn — `TurnNumber`, `PlayerId`, `Cash`,
   `NetWorth`, `PropertyCount`, `LoanTotal`, etc. Graphs ("money over time")
   then become one SQL query with no blob deserialisation. Same
   header-vs-snapshot pattern as §8, applied per turn.

---

## 10. Dice

1. **Dice behind an interface.** A single abstraction (e.g. `IDiceSource`)
   with a manual-input implementation for the first build.

2. **Virtual dice is a later implementation.** Adding it is a second
   implementation of the same interface — no engine change. When added, its
   RNG state (or the rolls it produced) must be captured in the snapshot so
   replay and backtrack stay deterministic (§8 rule 6).

---

## 11. Cards

The largest and riskiest sub-system — and the one previous attempts foundered
on. The decisive constraint:

1. **Cards are a fixed, predefined set.** Players cannot create their own.
   Previous attempts failed because *dynamic*, user-authored cards force a
   general-purpose effect interpreter/DSL. A closed set does not.

2. **One handler per card.** Each card is a concrete, hand-written,
   individually unit-tested implementation (a handler class, or a data row
   keyed to a known effect-type enum). The set is finite — cards are
   implemented and verified one at a time.

3. **An override pipeline for "unless a card states otherwise".** Many rules in
   `game-rules.md` defer to cards. The engine applies defaults through a hook
   pipeline that card effects can intercept — invoked only by *known* effects,
   never open-ended ones.

4. **NOPE cards need an interrupt model.** NOPE cancels a card and can be
   chained without limit (`game-rules.md` → Cards → NOPE). This requires a
   turn-pause / response model — simpler around one physical table, but still
   to be designed (§14).

5. **Card *contents* get their own authoritative doc.** `game-rules.md`
   deliberately excludes per-card contents (content, not rules). A separate
   `design-docs/card-decks.md` will list every card — deck, name, when
   playable, effect — and will drive card implementation the way `game-rules.md`
   drives the engine. To be written before cards are coded.

---

## 12. Ruleset Versioning

Snapshots outlive code: old games are loaded later, old turns are read for
stats, and the rules themselves change (several changed during the sessions
that produced `game-rules.md`).

1. **Snapshots carry a version.** Each snapshot records the snapshot-format
   version and the ruleset version.

2. **A game pins its ruleset.** A game finishes — and replays — under the rules
   it started with, so a rule change never retroactively alters an in-progress
   or completed game.

3. **Deserialisers are tolerant and migratable.** Format changes ship with a
   migration path for older snapshots.

---

## 13. Build Order / Roadmap

Incremental — each stage is usable and tested before the next.

1. **Persistence foundations.** Register `Game` in `AppDbContext` + migration
   (already the flagged next step in the session notes). Add the
   board/space/ownership representation the engine needs.
2. **Engine skeleton + money + basic turn loop.** Phase state machine, single
   player movement, third-die movement. No doubles/triples yet. Snapshot
   read/write working end to end.
3. **Doubles & triples.** Including the chaining/reset logic and per-value
   effects.
4. **Economy.** Loans, mortgaging, station price scaling, building rules — all
   formula-driven and highly testable.
5. **Complex space mechanics.** Free Parking resolution, purging, reserved
   properties.
6. **Cards.** Last and largest — preceded by `card-decks.md`.
7. **Snapshot history features.** Backtrack, replay, stats graphs (can begin as
   soon as stage 2's snapshots exist).

---

## 14. Open Decisions

Tracked here until resolved; resolutions fold back into this doc.

1. **`GamePlayer` split.** Which fields stay as queryable EF columns and which
   move into the snapshot (§5).
2. **Reserved Properties — "within a turn or two".** `game-rules.md` (Reserved
   Properties, rule 1) is not deterministic. The engine needs a concrete
   trigger for "every player is able to complete a set", or it becomes an
   explicit game-master adjudication step.
3. **NOPE interrupt model.** How a turn pauses for NOPE responses, and the UI
   for it (§11).
4. **Intra-turn recovery.** Whether per-turn snapshots are enough, or an
   intra-turn event log is also kept (§8 rule 4).
5. **`SetBoard` when a board was deleted.** Carried over from the session
   notes — snapshot the board into the game vs. fall back vs. throw. (The
   snapshot model in §8 largely answers this: a started game already carries
   its full board state, so a later board deletion cannot affect it.)

---

## 15. Traceability

1. **`game-rules.md`** — what the game does. The behavioural contract.
2. **`game-engine.md`** (this doc) — how the app implements it.
3. **`card-decks.md`** (to be written) — the contents of every card.
4. **Engine tests** — one or more per numbered rule, named after the rule.

A change to the game starts in `game-rules.md`, then propagates to the engine
and its tests in the same change.
