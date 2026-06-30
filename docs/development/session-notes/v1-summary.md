# V1 Development — Summary

A distilled, dated record of the **V1 build of Ultimate Monopoly**, from the first board-skin/game
models (2026-05-20) to **V1 code-complete** (2026-06-29). It captures the **key decisions and the
dev work that mattered** — not every detail. The full per-session context lives in
[`v1-dev/`](v1-dev/); where this summary and a session note disagree, the note (and the code) win.

> **The app is a *helper*, not a simulator** — a companion to a physical board game with a heavily
> custom ruleset (three dice, percentage cards, reserved properties, loans, Free Parking takes, turn
> tax, global events). It is split into **`MP.GameEngine`** (a pure, deterministic, snapshot-driven
> C# rules engine — no EF, no web) and the **`UltimateMonopoly`** ASP.NET Core 9 Razor Pages web app
> (persistence, SignalR, the social/admin platform on the `JC.*` packages).

**Status:** V1 is **code-complete** (Phases A–F + the A1 email-confirmation release gate, all in code).
The sole remaining launch step is deploy-time: registering the Microsoft Entra app on the server
(PowerShell → tenant/client id + secret into IIS env vars) and a staging pass. Out of V1 scope: the
optional **E3** cards reference page and the **V2** guides-content backend.

---

## The arc at a glance

| Part | Span | Theme |
|---|---|---|
| 1 | May 20–29 | **Foundations & frameworks** — board skins, the rules/engine design docs, the snapshot model, the prompt framework, turn-state, event receipts, the per-game executor, first live turn loop |
| 2 | Jun 2–10 | **Economy & a full game** — buy/auction/reserve, build/sell, loans, mortgages, deals, bankruptcy, game completion, the stats engine |
| 3 | Jun 11–19 | **The card sub-system** — the data-driven mini-engine: action services, the held-card trigger layer, immunity, all card decks authored, card stats |
| 4 | Jun 22–26 | **Review, V1 roadmap & the admin area** — the code-review pass, the roadmap, roles/profanity/blocking, and the full C1 admin area |
| 5 | Jun 27–29 | **Dashboard, messaging, polish & V1 ready** — admin dashboard, friend messaging, front-end/onboarding polish, Identity hardening, A1 |

Recurring threads throughout: the **working-copy / commit-at-the-turn-boundary** discipline (never
`SaveChanges` mid-turn), the **shared-reference footgun** in copy-ctors (mutate a fresh object, never a
shared definition), **read the engine/enums/field types before concluding**, and **review caught real
defects** in almost every session.

---

## Part 1 — Foundations & frameworks (May 20–29)

### 2026-05-20 — S1 — Board skins, sharing, first game models
- **Built:** board-skin editor (create→edit via PRG), a dedicated **Boards/Share** page + "Shared with Me", backed by `BoardSkinShareService` + a `SharedBoardSkin` join entity; John built the first game models — `Game`, `GamePlayer`, and `RuleDictionary` for rule constants.
- **Decisions:** outlined a future **game cache** (full game object, DB-synced per turn) as the fix for board staleness / revoked shares / deleted boards mid-game.

### 2026-05-20 — S2 — Game-engine architecture doc + snapshot models
- **Built:** completed `game-rules.md` (the full custom ruleset; per-card *contents* out of scope) and wrote `game-engine.md` (15-section architecture); John built `GameTurn` + `GameSnapshot` (1:1, `StateJson`) and moved live state out of `GamePlayer` into the snapshot JSON.
- **Decisions:** **helper not simulator**; the **rules engine is a separate pure library**; live state ≠ persistence entities; **one snapshot per turn, kept** (the one mechanism behind persistence/recovery/backtrack/replay/analytics); cards are a **fixed predefined set**; snapshot is taken at **start of turn**.

### 2026-05-21 — S1 — Game setup service + setup UI
- **Built:** `GameSetupService` (create/join/leave/kick/dice/reorder), join-by-code, the setup/lobby/game-list UI, all Games DbSets + migrations; extracted `UserService`.
- **Decisions:** dice-number uniqueness via a **DB unique index** (dice decide turn order); server-render now, SignalR later; **split the hub per lifecycle phase** (`GameSetupHub` + `GamePlayHub` over a shared base). Restructured in-game pages into a `Game` **area**.

### 2026-05-22 — S1 — Setup SignalR sync + MP.GameEngine begins
- **Built:** setup-phase SignalR end-to-end (membership-verified hubs, the five setup broadcasts) — **browser-tested, works**; John created the `MP.GameEngine` library + test project, the snapshot models, the polymorphic `EventReceipt` system, and `GameCacheModel`.
- **Decisions:** **one `GameModel`** serves as both live model and snapshot; board is fixed/cached per game (never snapshotted); **no ruleset versioning** (rule change = code change); `GameCacheModel` is **`DbContext`-style** (tracked working copy + `SaveChanges()` promote) with a **concurrency stamp**; engine is pure logic, the web does the wiring.

### 2026-05-23 — S1 — The prompt framework
- **Built:** the **prompt framework** — how the engine pauses, asks a player, and resumes: `Prompt`/`Prompt<TResponse>`/`PromptResponse`/`IPromptProvider`/`PromptValidator`, a single `PendingPrompt` slot, and the first concrete prompts (Interruptible window, Acknowledge, DiceRoll, AcquireProperty).
- **Decisions:** a pending prompt **need not survive a restart** (only the snapshot does) → inline `await` via `TaskCompletionSource`; the interruptible window is **host-Continue, not timeouts**; the **Metadata split** (game identity on the cache, per-turn state in `TurnMetadata`) shrinks snapshots and makes `HostPlayerId` single-sourced on the cache.

### 2026-05-24 — S1 — Turn-state + event-receipts frameworks + 103 tests
- **Built:** the rest of the prompt catalogue; **`TurnStateProvider`** (capability gates + named branched transitions); the **event-receipts framework** (`IEventEmitter`, the primitive-rich `FinancialTransactionReceipt` + 18-value `FinancialReason`); **103 framework tests, green**.
- **Decisions:** **extra turn fires from EndOfTurn** (so deals/bankruptcy fit between every roll); portfolio commands are **StartOfTurn-only**; receipts are **semantic-flavoured, primitive-rich** (drop redundant receipt types). Tests surfaced two real `PromptProvider` bugs (cancellation, double-submit) — fixed.

### 2026-05-25 — S1 — DI factory + snapshot pivot + first Start Game
- **Built:** the **`GameEngine` bundle + `IGameEngineFactory`** (resolves the per-game DI problem); `GameCacheService` owns cache lifecycle/hydration; **`TryStartGame`** wired end-to-end inside a caller-driven transaction.
- **Decisions:** **partial-snapshot pivot dropped** — every snapshot mints a new `GameTurn`+`GameSnapshot` pair (extra-turn rows share `CurrentPlayerId`), so the 1:1 schema stands with no migration; `TurnNumber` now counts turn-records.

### 2026-05-27 — S1 — Rule-service shape + TransactionService rewrite
- **Built:** the **three-layer rule-service cut** — pure data tables (`DoubleEffects`), primitive services (Movement/Balance/Card), and a `PlayerTurnOrchestrator`; consolidated `GameModel` primitives; **rewrote `TransactionService`** and wrote `transactions.md`.
- **Decisions:** designed the **per-game executor** (channel actor) to fix the SignalR prompt-deadlock; **engine error policy is two-mode** — reads/extractions **throw and bubble**, rule mutations **silently no-op**; `TransactionService` = one method per `FinancialReason`, **subject is always the payer**, `allowShortfall` is caller policy, `ShortfallOutcome` is tri-state (FundsRaised / DebtSettled / Bankrupted).

### 2026-05-28 — S1 — Per-game executor + engine→web seam + board partial
- **Built:** the **`IGameExecutor`/`GameExecutor`** (single-writer pump; `SubmitPrompt` stays out-of-band); the **`IEngineNotifier`** seam (`PromptOpened`/`PromptClosed`/`StateChanged`) + `SignalrEngineNotifier`; the `_BoardView` partial.
- **Decisions:** **live sync pushes the whole `GameCacheModel`** (not receipts; `Board`/`Events` `[JsonIgnore]`d) — receipts are history/stats only; **universal host-bypass** on the command gates (named player **or** host).

### 2026-05-28 — S2 — Live host Play page + critical turn-order bug
- **Built:** the **live host Play page** (`/Game/Play/{gameId}`, re-fetching the rendered partial on `StateChanged`), the in-game prompt JS framework, and the state-driven Roll/End-Turn commands — the turn loop **runs** end-to-end.
- **Notable:** 🔴 **critical turn-order bug** found — `GameModel.GetPlayers` never sorted by `OrderId`, so a 3-player game skipped a player; fix deferred to next session with a rotation test.

### 2026-05-29 — S1 — Test-driven bug fixes + GO/Jail services
- **Built:** `GameModel_Tests` (46) + `IndexHelper_Tests` (73); John built `GoService.CollectGoMoney`, extracted **`JailService`** (with the 50%-escalating fee), and the `LeaveJailPrompt`; heavy Play-page front-end pass (player tokens, on-board counters).
- **Decisions:** the **GO-pass contract** pinned (crossing/departing = pass; landing exactly on GO ≠ pass); leaving jail *whether* is a command, the pay-or-card settlement is a prompt.
- **Notable:** fixed the `GetPlayers` ordering bug, an `IndexHelper` infinite-loop, and a back-movement sign bug; removed 345 dead lines (`TransactionService_WIP.cs`).

---

## Part 2 — Economy & a full game (Jun 2–10)

### 2026-06-02 — S1 — Roll → move → land → buy works
- **Built:** the engine from "landing does nothing" to **roll → move → land → buy** end-to-end; the `TransactionService` test suite; **`AuctionService`** + `auction-flow.md`; property acquisition (buy/auction/reserve).
- **Decisions:** **`SaveChanges` belongs only at the turn-state boundary** (mutations accumulate on one `_working`, commit once) — the rule behind three stale-reference bugs; auctions are app-mediated, pass = out, min bid = 50% reserve, affordability-filtered up front (no auction shortfall); a mortgaged property is ignored for rent but **counts for station purchase scaling** (deliberate asymmetry).

### 2026-06-02 — S2 — Player-profile partial + host drawer
- **Built:** the in-game player-profile partial rendering live from `GameCacheModel`, and the host **player-profile drawer** reusing it (auto-open/close around prompts).
- **Decisions:** three standing directives — the partial **lives off `GameCacheModel`**; stubs still wear their real `Can…` guards; every money figure goes through `MoneyHelper`; prompts render as a modal **over** the open drawer.

### 2026-06-03 — S1 — Prompts as server-rendered profile state
- **Built:** the **Approach A refactor** — engine prompts now render **server-side inside the profile partial** from `cache.PendingPrompt` (killing the multi-prompt race); portfolio commands (un-reserve / mortgage / un-mortgage); generalised `AcquirePropertyPrompt` into a `Type`-driven confirmation.
- **Decisions:** player commands live on **domain services** (no `PlayerService` façade), flow **through the hub**; a prompt **cannot be closed, only answered**; loan instalment = 10% of outstanding originals, GO-only, oldest-loan-first, overpayment lost.

### 2026-06-04 — S1 — Build/sell, loans, shortfall seam
- **Built:** build + sell commands; the full **loan lifecycle** (`LoanService`); mortgage fee at GO; the shortfall + target-property prompt front-ends; the new `game-stats.md` persistence model.
- **Decisions:** the `TransactionService ↔ shortfall` DI cycle resolved via a **`ShortfallService`** reached through `engine.ShortfallService`; loans are **shortfall-only**; mortgage GO fee is **20% of purchase cost**; stats persist **raw receipts per turn** (`GameTurnEvents` blob), combed on demand, lifetime = finished games only.

### 2026-06-05 — S1 — Board branches + PropertyTransferService + rule citations
- **Built:** the remaining board-landing branches (Tax/GO/Jail/Free Parking, all card-seam-stubbed); **`PropertyTransferService`** (the title-side mirror of `TransactionService`); **rule citations** end-to-end (`RuleCode`, `rules.json`, `RuleCatalog`, the live "Rules Occurred this Turn" panel) + `rule-citation.md`.
- **Decisions:** `PropertyTransferService` is **title-only** (flips state + emits a receipt; leaves normalisation/reserve to callers); citations are a **sibling stream** — the engine emits stable semantic `RuleCode`s, the web resolves static text, and they **ship in the live frame**.

### 2026-06-08 — S1 — Deals + bankruptcy: a full game simulates
- **Built:** the **deals subsystem** (`game-deals.md`, `DealService`, the build/accept prompts, the Deal tab — two entry points → shared `RunDeal`); **bankruptcy + game completion** (`BankruptcyService`, `GameCompletionService`, `TransitionToFinalTurn`, the `GameCompleted` broadcast, the finished-game page); `GameTurnEvents` persistence at the turn boundary.
- **Decisions:** deals = **propose-is-command / accept-is-prompt**; dealable = owned/not-reserved/mortgaged-allowed/not-built-on; **cursed deals allowed**; an accepted shortfall deal → **`DebtSettled`**; the event blob persists in a **separate transaction** from the snapshot (deliberate, not atomic).
- **Milestone:** a **full game simulates end to end**.

### 2026-06-09 — S1 — Stats engine built + validated against a real game
- **Built:** the entire **engine-side stats projection** — all twelve `IStatsService` impls + `StatisticsOrchestrator` — **validated against a real 43-turn game JSON**; the voluntary-bankruptcy button.
- **Decisions:** the per-game-per-player summary is **materialised as `PlayerGameStat` at conclusion** (lifetime aggregation is O(games)); stats compute in the **pure engine** (`PlayerStatRecord`; the web's `PlayerGameStat` adds EF keys); fired from `ConcludeGame` + a recurring `MissingGameStatsJob`; net worth = cash + property value (mortgage value if mortgaged) + building **sell** value.

### 2026-06-10 — S1 — Host controls + stats comparison page
- **Built:** the game-lifecycle **host controls** (Force Refresh / Cancel / Delete); the recurring stats job; the all-players **stats comparison page** (`/Games/Compare`) on a shared **`StatCatalogue` + `StatRender`**.
- **Decisions:** Force Refresh / Cancel **broadcast off-pump** via `IEngineNotifier` (enqueuing would sit behind a parked prompt); Cancel is a plain POST; `StatCatalogue`/`StatRender` is the single source of truth for stat display; blocked players keep stats but mask identity.

---

## Part 3 — The card sub-system (Jun 11–19)

### 2026-06-11 — S1 — Cards become a working sub-engine
- **Built:** the **card mini-engine to a resolve-on-draw slice, live** — `cards-design.md` + `cards-actions.md` (Excel inventory: 202 actions / 170 cards); the model layer (`MoneyAction`/`MovementAction`/`JailAction`, polymorphic `CardAction`); the `CardService` interpreter; card-ID persistence (`PersistedCardIds`); a `TEMP_CARDS` standard deck.
- **Decisions:** **cards are data, not a DSL** (a closed action vocabulary keyed to effect enums); two modes (**held-card play** + **override-on-draw**); decks are persisted **`Queue`s** (order is the deck); `CardService` on the `GameEngine` bundle (breaks the `BoardService ↔ CardService` cycle); **immunity is reactive** (a prompted counter, like NOPE), not a silent flag.

### 2026-06-11 — S2 — Cards hardened into a working subsystem
- **Built:** `CardService` refactored into per-action **`ICardActionService<T>`** services + `CardActionHelper`; **`SuppressDefault`** override-on-draw wired into every board space; the held-card lifecycle closed via `PlayCard` (resolve → remove → return to deck); leave-jail-by-card; the Cards tab.
- **Decisions:** `ResolveCard` applies the effect only (where the NOPE/immunity window will later wrap); `DrawCard`/`PlayCard` are the two lifecycle wrappers; advance-to-GO releases the initial-GO lock via the **movement kind** (so the double-3 wobble stays locked).
- **Notable:** fixed the street-repair `(uint)` overflow, card fines going to Free Parking, and an infinite Get-Out-of-Jail-Free (held cards never consumed).

### 2026-06-12 — S1 — Action services finished + card-trigger design
- **Built:** all three per-action services finished (Money `EachPlayer` payer-loop, dice-off, `DiceMultiplier` via `DiceService.RollCardDice`, Movement `CollectGoBonus`); **dynamic card text** (`{G0__0}` tags); the card-option + target-player prompt front-ends; **designed the card-trigger architecture** (`card-triggers.md`).
- **Decisions:** triggers — `CardTriggerService` with one typed method per point, turn-scope = **is-the-holder-the-subject**, context amounts via **`MoneyAction.AmountSource {Fixed, TriggerAmount}`**, the card **does its own work** (the result only declares suppression), **Advance doesn't draw the destination card / `MoveSpaces` does**; **`GroupId` is the persisted card identity** (never key off display-only `GroupKey`).

### 2026-06-15 — S1 — Card actions built from the finalised list
- **Built:** John wrote the finalised **`cards.md`** (every deck); the `cards-dev-changes.md` worklist; **most remaining action services** (`TurnsActionService`, `DirectionActionService`, `LoansActionService`, `BuildingActionService` incl. `GrantHotel`, `PropertyActionService`, `GlobalEventActionService`, `DeckDrawActionService`) + Money/Movement/Jail extensions; the four standard-deck JSON imports (Chance / ComChest / %Chance / %ComChest).
- **Decisions:** this push is **action models + services + `CardService` wiring only** — the held-card trigger layer and NOPE/immunity are out of scope; the triple-bonus **payout** is cancellable/modifiable but the accumulator always **+£500** on a triple.

### 2026-06-16 — S1 — Dice, Tax, jail-lock families wired
- **Built:** the **Dice/triple-bonus family** (`DiceAction`/`DiceActionService`, the `ResolveTripleBonus`→`ApplyTripleBonus` split, `GameModel.ModifiedDiceRollType`, the orchestrator split into Normal/Double/Triple); a **generic dice-off** (`DiceOffPlayer`) shared across Money/Turns/triple-bonus; the **Tax/trigger-amount seam** (`AmountSource`, `CardActionContext.TriggerAmount`); jail-lock/collect-rent flags; global-event read-hooks wired live; the JSON decks persisted (migration); the **engine notification (toast) seam**.
- **Decisions:** `ICardActionService<T>.ResolveActionAsync` now returns `Task<bool>` and takes an optional `CardActionContext` (touches every action service); a converted-to-triple clears global events, a downgraded-to-double does not.

### 2026-06-16 — S2 — Card-JSON decks authored + deserialise bug
- **Built:** authored `third.json`, `double.json`, `triple.json`, `tax.json` one card at a time; engine additions the JSON forced — `CardTrigger.OnTaxLanded`/`OnSnakeEyes`, `NoOpAction` (suppress-only cards), `MoneyAction.Amount` made **`decimal`**, `PropertyActionKind.SwapSet`.
- **Notable:** 🔴 caught that **`SuppressDefault` was undeserialisable** (would have thrown on import of every Tax card + several Doubles/Triples) — fixed with a parameterless ctor; John reported undiagnosed **runtime card errors** to chase next session.

### 2026-06-17 — S1 — Trigger layer designed + rules pages
- **Built:** fixed the runtime card crash (the `CardModel` copy-ctor dropped `SuppressDefault`/`UniqueText`); built the full **`rules.json` catalogue** (117 entries) + the `/Rules` page, `/Leaderboard` + head-to-head Compare; **built `CardTriggerService` end-to-end** (per-trigger surface, the real trigger set derived from `cards.md`, the `EvaluatePlayableCards` loop).
- **Notable:** 🔴 found the **aliasing bug** — `EvaluatePlayableCards` used card #1's own `SuppressDefault` as the accumulator and `Aggregate`-mutated it in place, corrupting the shared definition (the recurring **shared-reference footgun**, third incident).

### 2026-06-18 — S1 — Trigger layer wired + last decks
- **Built:** fixed the aliasing bug (aggregate into a **fresh** object; triggers return `Task<SuppressDefault>`); audited **all 13 triggers wired at the correct subjects**; authored the last four decks (`go`/`justVisiting`/`freeParking`/`goToJail`.json) + the two deferred Third cards; the two `CardOptionPrompt` render modes; a large batch of primitives (`RequiredDirection`, condition `JailFilter`, `TwoDiceByThirdDie`, `ContextPlayer`, `NearestPlayerAhead`, the `CardTransfer` pass/steal category, befriend-a-guard, FP-pot floor at 0).
- **Decisions:** "anytime on your own turn" = **two windows** (`OnTurnStart` + `OnSpaceLand`); advance cards need `SuppressBoardResolution`; `PlayCard` removes the card **before** `ResolveCard` (self-retrigger fix); a kept card **no longer suppresses at draw**.

### 2026-06-18 — S2 — Eleven card bugs fixed; Immunity first cut
- **Built:** diagnosed (via parallel agents → file:line) and fixed **all 11 live-play card bugs** — forgotten/unconsumed `SuppressDefault` flags, jail-gating, purge target, multi-use flip-flop, a loan-repay ordering exploit.
- **Decisions:** **jailed holders are excluded from every trigger except `OnInJail`** (keeps GOOJF + befriend-a-guard); one-card-per-player-per-trigger is the real one-per-pass mechanism.
- **Notable:** reviewed John's first-cut **`CardImmunityService`** (5 hardcoded immunity types) — built but **not yet wired**; the standing risk is that suppress flags are **hand-authored with no engine guard/test**.

### 2026-06-19 — S1 — Immunity wired; card stats; runtime fixes
- **Built:** **Immunity wired end-to-end** (the 5 `Check*` hooks, `[JsonDerivedType("Immunity")]`, the 5 immunity cards authored); a batch of card-system runtime fixes (any-player trigger no longer fires on its own holder; "after your next move" no longer auto-fires on the drawing move via the `DrawnOnTurn` gate; GOOJF made command/prompt-only with `Trigger=None`); jail/bankruptcy fixes; the **card-stats feature end-to-end** (`CardStatsService`, per-type JSON-dict columns, the catalogue-driven `_PlayerStatsPartial`/`_CompareGame`); web/infra polish (rate limiting, friendly `/Error` pages, the current-turn bar).
- **Decisions:** GOOJF is identified by `JailAction{Kind=Release}` and is **command/prompt-only** (kept-in-hand like immunity); per-type card stats stored as **JSON dict columns**.
- **Notable:** NOPE remains deferred (V2, possibly scrapped); immunity is the built one-shot cousin.

---

## Part 4 — Review, V1 roadmap & the admin area (Jun 22–26)

### 2026-06-22 — S1 — Code-review resolution + the V1 roadmap
- **Built:** worked an entire static code review end-to-end (every R/H/M/L item) plus John's **8 live-play card/runtime bugs** (R-01–R-07 fixed, R-08 deferred); reorganised all docs under `/docs`; wrote `docs/development/README.md` and **`v1-roadmap.md`**.
- **Decisions:** **all cards playable in jail** (R-02, reverses the 18th's jail-filter); **any active player may deal** at a turn boundary (M-03); **board skins only rename spaces** (not prices/layout); `HasBeenPurged` removed (`IsPurged` is the sole flag); rate limiting set to explicit TokenBucket (30 burst / 15 sustained).

### 2026-06-22 — S2 — V1-roadmap execution: roles, profanity, Manage rewrite
- **Built:** **A2 roles + `Restricted`** finished (seeded via `ConfigureAdminAndRolesAsync`, authoritative server-side guards); **E2 hidden-profile** as a role (`HiddenUser`), non-friends-only; **B1 profanity filter** end-to-end (`ProfanityService` over `Profanity.Detector` + a DB `BlockedWord` list, `ProfanityNormaliser`); the **`/Identity/Account/Manage` 3-tab rewrite** (Account / Security / Personal Data) with the editable display name; personal-data export widened.
- **Decisions:** **A1 (email confirmation) moves to the release gate R** — done last (paid Entra prerequisite); the `Restricted` boundary fixed (no friend requests / messages / create games / create-or-share board skins); B1 biases to **under-block + admin override** with a generic user message.

### 2026-06-23 — S1 — C1 admin area: shell + User Management
- **Built:** `c1-admin-area.md`; the **`Areas/Admin` shell** (two-tier `AdminArea`/`SystemAdminOnly` auth, layout + sidebar) with **`AdminActionLog`** (an immutable record of every admin action); **User Management** (list + details); the user-facing **Disabled Account** page.
- **Decisions:** **two-tier auth** (Admin = read+moderation, SystemAdmin = config+destructive); **admin code lives inside `Areas/Admin`**; user delete is a hard delete leaving orphaned-by-id records (a deferred `OrphanCleanupJob`); bind paginated params as **`pageNumber`** (never the reserved `page`).

### 2026-06-24 — S1 — C1: Reports, Rules/Turn-Tax, Game Management
- **Built:** tiered-auth hardening + peer-moderation; **role-change propagation** (`AuthRefreshService` + `AuthRefreshMiddleware` — refreshes only the requester's own session); **Reports** (a `ReportResolution` `[Flags]` queue + quick actions); the **Rules + Turn Tax** editors; the **Game Management list** and the big **read-only Game Details** state render (reusing the live in-play partials).
- **Decisions:** **background jobs → a dedicated final phase** (build the synchronous half now, don't re-flag the missing job); the read-only flag is **`IsAdminView` on the model** (ViewData doesn't reach a handler-returned partial); reused partials need **absolute** nested-partial paths cross-area.

### 2026-06-25 — S1 — Game Management finish + the snapshot dedup + audit trail
- **Built:** the read-only state panel + wired game lifecycle actions + snapshot-size reporting + a turn-revert "danger zone"; the **JC.Core `EntityKey` migration** (unblocks the audit data trail); **C1 Phase 6 Audit User Trail**.
- **🌟 The big one — snapshot card dedup (~200KB → ~12KB/turn):** decks store **`Queue<string>` card ids**, hands hold thin **`PlayerCardInstance`s**, full `CardModel`s come from a new **`ICardCacheService`** on demand, and **`Take` always clones** so game state never aliases the shared cache — a ~16× cut with no fidelity lost.
- **Decisions:** revert-to-turn-N re-plays N (delete later turns + N's events row, keep its snapshot) and tears down the live runtime.

### 2026-06-25 — S2 — C1 Audit Data Trail
- **Built:** the **Data Trail** (Index → per-table history), and made the shared `_AuditTable` partial **dual-mode** (the variable column flips Table ↔ User by scope).
- **Decisions:** the Data-Trail index isn't searched/paginated (the table set is small + bounded); search scope is mode-dependent (user trail searches table+key, data trail searches user+key).

### 2026-06-26 — S1 — C1 Phase 7 Logs + bug-reporter + GithubManager
- **Built:** the **log viewers** (Email + Messaging) + a **Reported Issues** page over **JC.Github**; a full **bug-reporter / feedback** system (a dev floating widget + an all-env "Give Feedback" modal, both → `/BugReport`); the tightly-scoped **`GithubManager`** role; planned the Recent Activity panel (§7.4).
- **Decisions:** **no user/client metadata on the public GitHub repo** — it's threaded through JC.Github and stored on the `ReportedIssue` record, surfaced only in the admin accordion; **two bug reporters by design**; the GithubManager redirect must live in **middleware** (role-guarded service constructors run before page filters).

---

## Part 5 — Dashboard, messaging, polish & V1 ready (Jun 27–29)

### 2026-06-27 — S1 — C1 finished + the admin Dashboard
- **Built:** the **Issue Contact** flow; the four **game-retention Hangfire jobs** (`GameCleanupJob`/`GameAbandonmentJob`/`CancelledGameCleanupJob`/`SnapshotCleanupJob`) — the last C1 gap; the entire admin **Dashboard** as **hub-and-spoke** (5 tiles → 5 spokes: Users / Community / Games / Audit / App Logs) + `c1-admin-dashboard.md`.
- **Decisions:** dashboard = hub-and-spoke (not one page); Community folds in moderation; ops health stays on `/hangfire` (no widgets); v1 metrics are all cheap-live (no precompute job). **C1 is essentially complete** (phases 1–8 + the retention layer + the Dashboard cap).

### 2026-06-28 — S1 — E1 friend messaging, end to end
- **Built:** **E1 friend messaging** — DM-only, friends-only chat on `/Social/Messages` with **live SignalR delivery** (`MessagingHub`, push-only via `Clients.User`), all guards in **`FriendMessagingService`** (friends-only / not-blocked / `Restricted`-read-only), unread badges + "Read @time" receipts from the package; the mock `Friends/Chat` retired; `e1-friend-messaging.md`.
- **Decisions:** every guard lives in **our service, never the package/hub**; chats start from a profile/friends-list button only; **game invites split out as the deferred E1b**.

### 2026-06-29 — S1 — V1 code-complete: Phase F + Identity hardening + A1
- **Built:** the deferred infra (`AdminLogCleanupJob` + **all package log-retention jobs registered**); **Phase F** — F1 home redesign (adaptive public landing + signed-in hub), F2 the **mega-dropdown navbar**, F3 the public **`/Guides`** page (Quick Start + Contact); the **Declare Winner** host control; the first-run welcome card; **Identity hardening** — `RequireConfirmedAccount`, the password policy (≥8 + complexity), `RedirectAuthenticatedFilter`, restyled confirm-email pages (scaffold backdoor removed).
- **Decisions:** the width wrapper is `.app-page` on `<main>` (no Bootstrap containers); onboarding is a hub welcome card (auto-retire), not a banner; the guides **content** system is V2; server-only auth-page errors use the `string.Empty` key.
- **🌟 Milestone — V1 is code-complete.** The roadmap reads **A–F complete**; **A1 flips to 🟢 (code)** with only **Microsoft Entra tenant provisioning** (deploy-time IIS env-var config) outstanding. Dev uses the Console email provider until then.

---

## Where V1 landed — carried debts (as recorded in the notes)

State as of code-complete; **verify against current code before acting** (several may already be done):

- **R-08** — suspected orchestrator/dice-modifier quirks; **deferred to V1.x+** pending a unit/simulation test harness (unconfirmed).
- **Engine unit tests** — deliberately not kept in lockstep as the engine outpaced them (e.g. the `TransactionService_Tests` failures); a one-person-band trade-off.
- **`OrphanCleanupJob`** — the one deferred housekeeping job (not a launch blocker; the social/game tables use no-FK string user ids by design).
- **Suppress-flag invariant** — `SuppressDefault` flags are hand-authored with no engine guard/test (the cause of several forgotten-flag card bugs); a lockstep/invariant check is the candidate hardening.
- **NOPE** — out of V1 (V2, possibly scrapped); immunity (its one-shot cousin) is built and in.
- **EF migrations flagged at write-time** (e.g. `MostLandedOnBoardIndexCount`, the card-stats columns, `RegisteredUtc`) — the repo's global `dotnet-ef` is too old for net9, so they're applied from the IDE.
- **Data caveats** — stats that depend on a receipt added mid-build (immunity counts, jail-release, card `DiceRoll`s) only populate for games serialised after the change; recompute can't backfill.

---

## Related docs

- `design-docs/v1-roadmap.md` — the road to V1 (phases A–F + the A1 release gate; the authoritative status).
- `design-docs/game-design/` — `game-rules.md` (the behavioural contract), `game-engine.md`, `transactions.md`, `game-deals.md`, `auction-flow.md`, `rule-citation.md`, `game-stats.md`, `game-ui.md`.
- `design-docs/frameworks/` — `turn-state.md`, `choice-events.md` (the prompt framework), `event-receipts.md`.
- `design-docs/` (root) — `web-orchestration.md` (commands + the single-writer executor), `signalr-design.md`, `c1-admin-area.md`, `c1-admin-dashboard.md`, `e1-friend-messaging.md`.
- `design-docs/cards/` — `cards-design.md`, `card-triggers.md`, `cards.md`, `cards-actions.md`, `cards-dev-changes.md`.
- `v1-dev/` — the full per-session notes this summary distils.
</content>
</invoke>