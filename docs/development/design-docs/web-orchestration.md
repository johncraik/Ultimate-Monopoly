# Web Orchestration — Commands, Services & the Per-Game Executor

How the **web layer** drives the engine: the command pipeline (player-initiated
actions), which web services own what, and how everything funnels through the
single-writer executor. This is the *command* counterpart to `choice-events.md`,
which owns the *prompt* (engine-initiated, mid-execution) side. `choice-events.md`
§2 explicitly defers the command pipeline to "a separate system, not covered by
this doc" — this is that doc.

**Status:** implemented (and still evolving). The per-game executor
(`IGameExecutor`/`GameExecutor`), the `IEngineNotifier` broadcast, the
host-bypass gates on `TurnStateProvider`, and the command/query services
described here are built. Some items the original status listed as "not yet
written" (game lifecycle, end turn, player commands) have since landed. See the
drift note at the foot of this document.

---

## 1. The boundary: commands vs prompts

Two directions of player interaction, two mechanisms (per `choice-events.md` §2 /
`turn-state.md` §2):

| Direction | When | Mechanism | Owner |
|---|---|---|---|
| Player → engine | Engine **idle** (turn boundary / pre-roll) | **Command** | Web services (this doc) |
| Engine → player | Engine **mid-execution** | **Prompt** | Prompt framework (`choice-events.md`) |

**Commands** are "the player clicked a button" actions when nothing is running —
mortgage, build, sell, propose a deal, leave jail, declare bankruptcy, end turn.
**Prompts** are what the engine emits while running — dice, buy/decline, auction
bid, shortfall, NOPE. The web services in this doc handle commands only; prompts
are answered via `GamePlayHub.SubmitPrompt` and rendered in the player drawer.

---

## 2. The per-game executor is the single writer

Every action that **mutates game state** runs on the game's executor pump
(`IGameExecutor.Enqueue`) — one writer per game, so the cache's working-copy model
is never raced. Hub methods **enqueue and return**; the work runs off the
connection thread.

1. **Commands enqueue.** A command handler enqueues a work item that (re)checks
   the gate and then runs the relevant rule service / orchestrator.
2. **`SubmitPrompt` is the only out-of-band path.** It does not enqueue — it
   resolves a *parked* prompt's awaiter directly, unblocking the in-flight work
   item rather than deadlocking behind it. See `choice-events.md`.
3. **Turn kickoff is a command too.** `GameService.EnqueueTurn(gameId)` enqueues
   `StartPlayerTurn` → `ResolveThirdDieMovement` as one work item; the turn then
   rests at `EndOfTurn` awaiting the player's End Turn command.

---

## 3. Service responsibilities

### `GameService` — the web orchestrator
The game-level seam. Owns:
- **Cross-game queries** — the list pages (my games / joined / by state).
- **Membership** — `CheckUserInGame` (used by the hubs' group auth).
- **Game lifecycle** *(planned)* — load, delete, cancel.
- **Game-progression actions** — `EnqueueTurn` (built); **end turn** *(planned)*
  — actions that move the game forward rather than manage one player's portfolio.

### `PlayerProfileService` — player-specific commands
A single player acting on their own holdings, engine idle. Owns:
- **Per-player queries** — `GetPlayerForGameSetup` / `GetPlayerForGamePlay` (built).
- **Player commands** *(planned)* — declare bankruptcy, mortgage / unmortgage,
  build, sell buildings, unreserve, propose / accept a deal, play a card from
  hand, choose how to leave jail.

### The prompt framework — not a service
Engine-initiated prompts are not handled by a web service. They flow through
`IPromptProvider` (engine) ↔ `GamePlayHub.SubmitPrompt` (web) and render in the
player drawer. Listed here only to complete the picture.

### Command → gate mapping
Each command group is gated by a `TurnStateProvider.Can…` method before its work
runs:

| Command(s) | Gate | Service |
|---|---|---|
| mortgage / unmortgage / build / sell / play card / pay loan early / unreserve | `CanPortfolioCommand` | `PlayerProfileService` |
| propose / accept deal | `CanDeal` | `PlayerProfileService` |
| jail exit (pay fee / play card / attempt double) | `CanLeaveJail` | `PlayerProfileService` |
| declare voluntary bankruptcy | `CanDeclareBankruptcy` | `PlayerProfileService` |
| end turn | `CanEndTurn` | `GameService` |

---

## 4. Host authority is universal

The host tablet is the game controller; phones are an optional convenience layer
(`Game-UI.md`). Every command method takes `(targetPlayerId, submittingUserId)`,
and the gate authorises **the named player or the host** — the host-bypass on
`TurnStateProvider`'s gates and in `PromptValidator`. So the identical command
path serves a player on their phone and the host driving it from that player's
drawer; the only difference is who `submittingUserId` is.

---

## 5. Gate evaluation belongs on the writer thread

`Can…` reads live cache state (`TurnState`, `PendingPrompt`), which can shift
between an optimistic pre-enqueue check and execution. The **authoritative** gate
check therefore happens **inside the work item on the pump** (single-writer,
consistent), not only at the hub before enqueuing. A cheap pre-check at the hub is
fine as an early-out, but it is not the source of truth.

---

## 6. Live state sync (the output side)

Driving the engine is only half the loop; clients also need to see the result.

- **`IEngineNotifier.StateChanged(cache)`** broadcasts the whole `GameCacheModel`
  (with `Board` and `Events` `[JsonIgnore]`d) to the `game-play__{gameId}` group,
  at two key points: when a prompt opens (the prompt seam) and when a work item
  completes (the executor). `ConcurrencyStamp` is the version.
- **Acknowledge prompts are the de-facto sync heartbeat.** Almost anything that
  *happens to* a player opens an `AcknowledgePrompt` (largely why they exist — to
  mediate the UI/engine handshake), so the prompt-open push fires throughout a
  turn and the live view stays near-current without per-commit pushes.
- **No push at prompt-*clear*** — deliberate: the parked work item resumes
  *asynchronously*, so a clear-time push would broadcast the state *before* the
  prompt's consequence is applied (e.g. the balance before the £50 charge). The
  post-consequence state arrives on the next prompt-open or the completion push.
- **`GamePlayHub.GetBoard`** serves the static board once on connect.
- **Receipts are not used for the live view.** They are internal history and the
  per-turn / per-player statistics source; the live view renders from current
  cache state, never by replaying the receipt stream. (`event-receipts.md` §6/§8
  still describe a live-narration role — that text is stale and predates this
  decision.)

---

## 7. Traceability

1. **`choice-events.md`** — the prompt framework; §2 draws the commands-vs-prompts
   boundary this doc's commands sit on, and defers the command pipeline here.
2. **`turn-state.md`** — the `Can…` capability gates (now host-bypass aware) and
   the transitions commands drive.
3. **`Game-UI.md`** — the host-is-controller / phones-optional model that makes
   host-bypass universal, and where each prompt/command surfaces.
4. **`event-receipts.md`** — receipts as internal history/stats (§6/§8 pending a
   correction re: live narration).
5. **Code** — `Services/Games/GameService.cs`, `Services/Games/PlayerProfileService.cs`,
   `Services/GameEngine/GameExecutor.cs`, `Services/GameEngine/SignalrEngineNotifier.cs`,
   `Hubs/GamePlayHub.cs`, `Services/Framework/TurnStateProvider.cs`.

---

## Implementation status & drift

> This document records the **agreed design**, not the live state of the code.
> Since it was written the implementation has moved on — much of what is
> described here is built, and some details have changed. Any status, "TODO",
> "not yet built", or "pre-implementation" note above may be out of date.
>
> Where this doc and the code disagree, the **code (and the developer) win**
> (`docs/development/README.md`). Verify specifics against the current code.