# Ultimate Monopoly code review — 2026-06-20

## Scope
- Source: `Ultimate-Monopoly-master (2).zip`.
- Reviewed statically: ASP.NET app, game engine, SignalR hubs, cache/executor, cards, prompts, turn flow, transactions, board skins, stats, tests, docs/session notes.
- Build/test status: **not run**. Sandbox has no `.NET SDK` (`dotnet: command not found`). Findings below are static-analysis findings only.
- Tests present: `MP.GameEngine.Tests` only, 6 test files, 189 `[Fact]`/`[Theory]` entries. No web/integration/UI test coverage found.

## Architecture map
- Setup: `GameSetupService.TryStartGame` -> `GameEngineSetupService.SetupGameCache` -> `SnapshotService.CreateSnapshotAsync` -> `GameCacheService.PopulateGame` -> `GameService.EnqueueTurn`.
- Runtime: `GamePlayHub` optimistic checks -> `GameExecutor` per-game pump -> scoped `GameEngineFactory` -> engine service -> `TurnStateProvider` commits snapshots/events.
- Prompts: engine opens `PromptProvider.RequestAsync` -> clients submit via `GamePlayHub.SubmitPrompt` -> direct TCS resolution, bypassing pump by design.
- Cards: `CardCacheService` -> `CardImportService` -> decks on game cache -> `CardService.DrawCard/PlayCard` -> action services -> event receipts -> stats.
- Completion: engine/game service -> `GameCompletionService.ConcludeGame` -> DB outcome/user counters -> Hangfire `StatisticsJob`.

## Critical / high findings

### H-01 — Game completion clears live runtime before DB commit ✅ FIX IMPLEMENTED
- **Where:** `UltimateMonopoly/Services/GameEngine/GameCompletionService.cs` — `ConcludeGame`.
- **Evidence (confirmed in current code):** `ClearGameRuntime(gameCache.GameId)` invalidated the cache / stopped the pump *before* `BeginTransactionAsync` and the `SaveChangesAsync`/`CommitTransactionAsync`.
- **Impact:** if the commit rolled back, the in-memory game was destroyed while the DB still said in-play → next access rehydrates from the old snapshot (zombie/replayed game).
- **Fix:** moved `ClearGameRuntime` to **after** the successful commit (placed just before the stats-job enqueue + `GameCompleted` notify, which were already post-commit). The runtime is now torn down only once the game is durably `Finished`; if `EndGame` or the commit throws, the runtime is deliberately left intact (game not finished). Preserved: the pump stop stays fire-and-forget (`ConcludeGame` runs on that pump's own work item — awaiting it would deadlock), and the local `gameCache` reference remains valid for outcome determination after cache eviction. This mirrors the cancellation path's "tear down after DB update" ordering. Web builds clean (0 errors).

### H-02 — Board skin cache invalidation is effectively blocked for normal users ✅ FIX IMPLEMENTED
- **Where:** `UltimateMonopoly/Services/Cache/BoardCacheService.cs`; callers in `BoardSkinService.cs` (×6) and `BoardSkinShareService.cs`.
- **Evidence (confirmed):** `Invalidate(userId)` returned immediately when `userId` was non-empty and the caller wasn't SystemAdmin, and every caller passed an explicit id — so a normal user could never clear their own cache after creating/editing/deleting/sharing a board skin (stale list for up to 6h).
- **Fix:** `Invalidate(string? userId = null, bool bypassAdminCheck = false)` — own cache is the no-arg form (defaults to the current user, no guard); invalidating **another** user's cache still requires admin; a `bypassAdminCheck` trusted overload covers the legitimate cross-user case. Callers updated:
  - `BoardSkinService` create/edit/delete (×6) → `Invalidate()` (own cache).
  - `BoardSkinShareService.TryRemoveSharedBoardSkin` → `Invalidate()` (recipient clearing their own cache).
  - `BoardSkinShareService.TryShareBoardSkin` recipient loop → `Invalidate(userId, true)` — the trusted bypass, so the (non-admin) owner refreshes each recipient's board list immediately on share.
- Web builds clean (0 errors). *(BoardCacheService signature + 7 caller updates were John's; the `TryRemoveSharedBoardSkin` own-cache caller was the one remaining miss.)*

### H-03 — `MovementStatsService` can throw when a player has no counted landings ✅ FIX IMPLEMENTED
- **Where:** `MP.GameEngine/Services/Statistics/MovementStatsService.cs`.
- **Evidence (confirmed):** `record.MostLandedOnBoardIndex = landOnIndexes.MaxBy(kv => kv.Value).Key;` — `landOnIndexes` is a `Dictionary<ushort,uint>`, and `MaxBy` on an empty sequence **throws** for a value-type element (`KeyValuePair`), so a player with no counted landings sank the whole game's stats projection (`StatisticsJob` catches + logs → no stats for the game).
- **Fix:** guarded with `landOnIndexes.Count > 0 ? MaxBy(...).Key : IndexHelper.GoSpace`. `MostLandedOnBoardIndex` is a non-nullable `ushort` (record + DB column), so a `null` would have needed a migration; the sentinel `Go` (index 0) is used instead — its land count is already `0` in the same record, so the row stays internally consistent. Engine builds clean (0 errors).
- **Follow-up:** a regression test (movement stats with no landings does not throw) is still worth adding — deferred with the broader test-scaffolding work.

### H-04 — Card import silently ignores missing/empty card files ❌ WON'T FIX (BY DESIGN)
- **Where:** `UltimateMonopoly/Services/Imports/CardImportService.cs`.
- **Evidence:** missing file and empty/null deserialisation both `continue`.
- **Resolution (John):** intended behaviour, not a defect. A missing card JSON is tolerated by design — no card file means no cards of that type, and the game is **still fully playable without cards**; no throw is wanted. May revisit later (e.g. an opt-in "required decks" check) but not now.

## Medium findings

### M-01 — `CardOptionPrompt` validation accepts any key for play-card choices ✅ FIX IMPLEMENTED
- **Where:** `MP.GameEngine/Services/Framework/PromptValidator.cs` + `CardImmunityService.cs`.
- **Evidence (confirmed):** `return prompt.PlayCardChoice || prompt.Options.Any(o => o.Key == r.SelectedKey);` — when `PlayCardChoice` is true the `||` short-circuits, so *any* `SelectedKey` validated. And `CardImmunityService` played its found `immunityCard` on any non-empty key without comparing to `immunityCard.CardId`.
- **Fix (both ends):**
  - **Validator:** `PlayCardChoice` now accepts only an empty decline **or** a key present in `Options`; a mandatory choice (group pick / card steal, `PlayCardChoice` false) still must name a real option. Bogus keys rejected.
  - **Immunity service (defence-in-depth):** plays only when `response.SelectedKey == immunityCard.CardId`; empty or any other key is a no-play.
- **No regression:** the real play-card flows send either a valid option id (the card) or an empty key (decline) — both still accepted (`CardTriggerService.PromptForCard`, the immunity prompt, the single-card yes/no UI). Engine builds clean (0 errors).

### M-02 — Game cache hydration is not single-flight ✅ FIX IMPLEMENTED
- **Where:** `UltimateMonopoly/Services/Cache/GameCacheService.cs`.
- **Evidence (confirmed):** `GetGame` did `TryGetValue` then `HydrateAsync` with no per-game serialisation, so two concurrent cache misses for the same game each built and cached a separate (mutable) `GameCacheModel` — split-brain where the pump mutates one instance while a hub prompt/state-read holds another.
- **Fix:** single-flight via a `static ConcurrentDictionary<string, SemaphoreSlim>` per-game gate. `GetGame` keeps the fast cache-hit path, then takes the per-game gate and **double-checks** the cache inside it, so only the first miss hydrates and waiters get the cached instance. (Board hydration `GetGameBoard` is left as-is — boards are immutable, so a duplicate build is harmless.)
- **Lock lifecycle:** `Invalidate(gameId)` removes the game's gate from the dictionary (bounds growth, ties lock lifetime to the working copy). Deliberately **not** disposed — a `SemaphoreSlim` used only via `WaitAsync`/`Release` allocates no wait handle, and disposing one a concurrent in-flight hydrate still holds would throw on `Release()`. *(Lock-removal-on-invalidate was John's add.)*
- Web builds clean (0 errors).

### M-03 — Deals are documented as any-player-at-boundary, but code restricts to current player ✅ FIX IMPLEMENTED
- **Where:** `MP.GameEngine/Services/Framework/TurnStateProvider.cs` (`CanDeal`); hub contract in `GamePlayHub.ProposeDeal`.
- **Evidence (confirmed):** the hub comment said the proposer need not be the current player, but `CanDeal` required `IsCurrentPlayer(playerId)` — so non-current players couldn't propose at a boundary.
- **Decision (John):** the hub contract is the intended rule — **any active player may propose a deal at a turn boundary**, not just the current player.
- **Fix:** removed `IsCurrentPlayer` from `CanDeal` and added an `IsActivePlayer(playerId)` guard (`GetPlayer(playerId) != null`, i.e. exists and not bankrupt). `CanDeal` doc updated to match; the hub comment was already correct so left as-is. The active-guard was also added to `CanStartTurn` and `CanDeclareBankruptcy` for consistency. Engine builds clean (0 errors).

### M-04 — `GameExecutor` queue is unbounded ✅ FIX IMPLEMENTED
- **Where:** `UltimateMonopoly/Services/GameEngine/GameExecutor.cs` (`GamePump`).
- **Evidence (confirmed):** `Channel.CreateUnbounded<GameWorkItem>` — repeated hub commands while a prompt is parked grew the per-game queue without bound.
- **Fix:** bounded channel, `GamePump.Capacity = 10` (`BoundedChannelFullMode.Wait`). The cap counts only **pending** work — a running item is already dequeued, and a faulted pump discards its queue — so 10 commands behind the in-flight one is far beyond legitimate turn-based play (a saturated queue means a parked/wedged turn).
- **Closed-vs-full disambiguation:** the `Enqueue` retry loop treated *any* `TryEnqueue` failure as "pump shutting down → drop & recreate". Since `TryWrite` returns false for both *full* and *completed*, `TryEnqueue` now returns a tri-state `EnqueueOutcome` (`Enqueued` / `Full` / `Closed`), backed by a `_closed` flag set before the channel is completed (fault / dispose). `Enqueue` then: `Enqueued` → return; `Full` → log + drop (backpressure; the on-pump gate re-check would reject it anyway and the sweeper reclaims a wedged pump); `Closed` → drop the pump and loop to spin a fresh one.
- Web builds clean (0 errors).

### M-05 — Statistics are write-once and cannot self-correct after stats logic fixes ❌ WON'T FIX (BY DESIGN, for now)
- **Where:** `UltimateMonopoly/Services/Statistics/StatisticsJob.cs`.
- **Evidence:** skips a game if all player stat rows already exist; only fills missing rows.
- **Resolution (John):** intended for now. After a stats-logic fix, the recompute path is **manual record deletion + re-firing the Hangfire job** (which then re-fills the now-missing rows) — sufficient at this stage. An admin "refresh stats" surface (versioned schema / explicit recompute mode) may be added later, but isn't warranted yet.

### M-06 — Jail exit stats misclassify card exits as dice exits ✅ FIX IMPLEMENTED
- **Where:** `MP.GameEngine/Services/Statistics/JailStatsService.cs` + `CardPlayedReceipt`.
- **Evidence (confirmed):** `const int leftByCard = 0` TODO — `TimesLeftJailByPlayingCard` was always 0, and the unaccounted Get-Out-of-Jail-Free exits inflated `TimesLeftJailByDice` (the leftover bucket).
- **Fix:** the card-play receipt now exists, so added an `IsJailRelease` flag to `CardPlayedReceipt` — computed in the ctor like `IsImmunity` (the chosen group contains a `JailAction{Kind=Release}`). `JailStatsService` now counts `CardPlayedReceipt`s flagged `IsJailRelease` for the player as `leftByCard`, so the dice bucket (`totalLeaves − paying − card`) is correct.
- **Data caveat:** `IsJailRelease` only populates for games whose receipts were serialised **after** the flag was added — older games read 0 (that exit falls into the dice bucket) and a recompute can't backfill an unstored field. Same shape as the immunity-count caveat; verify on a fresh game.
- Engine builds clean (0 errors).

### M-07 — Card dice rolls are not counted in movement stats ✅ FIX IMPLEMENTED
- **Where:** `DiceService.RollCardDice`; `MovementStatsService`.
- **Evidence (confirmed):** card rolls emitted no `DiceRollReceipt`; `cardRolls += 0` TODO → `TotalCardRolls` always 0.
- **Decision (John):** card rolls *should* emit a `DiceRollReceipt` — reusing the existing receipt is safe because a card roll is never a turn roll (no third die).
- **Fix:**
  - `RollCardDice` now emits a `DiceRollReceipt` for the card roll, and clamps `diceCount` to ≤2 so the "never a 3-dice turn roll" invariant always holds (`IsTurnRoll` stays false, `RollType` Normal).
  - `MovementStatsService`: `cardRolls` now counts the non-turn rolls (`!IsTurnRoll`). The `turnRolls` and doubles/triples counters were already safe (turn rolls are 3-dice; card rolls are always `Normal`). The **dice-number** stat — which keys on the two main dice and could otherwise false-match a 2-die card roll — is now gated to `IsTurnRoll && RollType != Triple`.
  - Engine-side alignment (John): `PlayerModel.IsDiceNumber` now also returns false for a `Triple`, so the runtime "rolled your number → third card" check and the stat agree (a triple is its own mechanic, not a dice-number hit).
- Engine builds clean (0 errors).

### M-08 — Card persisted IDs are order-dependent ❌ WON'T FIX (BY DESIGN)
- **Where:** `UltimateMonopoly/Services/Imports/CardImportService.cs`.
- **Evidence:** `UniqueText = rawText + [[globalIndex]]`; global index increments by import order across files.
- **Resolution (John):** the index-based identity is fine as-is. Card JSON edits already require a fresh game to take effect, so the cross-game continuity concern doesn't bite in practice; no change needed.

### M-09 — Card model mismatch exceptions lack file/card context ✅ ALREADY RESOLVED (stale review)
- **Where:** `UltimateMonopoly/Services/Imports/CardImportService.cs`.
- **Resolution:** already fixed before the review — the review was run against a stale zip of the codebase. The context-less `Group/Action/Condition count mismatch` **throws no longer exist**: the current `TryPopulate` **returns `false`** on a group/action/condition count mismatch (graceful — the card simply isn't matched to its persisted IDs) instead of throwing a bare exception. Verified against the live code; no change needed.

### M-10 — `CardCacheService.GetCard` nullable contract is false ✅ FIX IMPLEMENTED
- **Where:** `UltimateMonopoly/Services/Cache/CardCacheService.cs`.
- **Evidence:** method declared `Task<CardModel?>` but threw `InvalidOperationException` when not found.
- **Fix (John):** signature changed to non-nullable `Task<CardModel>` — the method is a required-getter (throws if the card id is unknown), so the type now matches the behaviour and callers no longer get a misleading nullable contract.

## Low / polish findings

### L-01 — Program rate-limit comment and value disagree ✅ FIX IMPLEMENTED
- **Where:** `UltimateMonopoly/Program.cs`.
- **Evidence:** comment said 10 req/min; `PermitLimit = 20`.
- **Deeper finding (per JC.Web docs):** the config was leaning on misleading defaults. Under `TokenBucket`, `PermitLimit` is a **no-op** — capacity comes from `TokenLimit` (which, being `0`, fell back to `PermitLimit = 20`) and refill from `TokensPerPeriod` (defaulting to **10**). So the *actual* behaviour was burst 20 / sustained 10-per-min — neither comment was right.
- **Fix (intent confirmed with John — auth endpoints need to tolerate legitimate bursts: wrong-password retries, register→login, confirm-email, 2FA, shared NAT/CGNAT IPs):** set the token bucket **explicitly** — `TokenLimit = 30` (burst), `TokensPerPeriod = 15` (sustained 15/min), `Window = 1 min`, per `ClientIp`; removed the no-op `PermitLimit`. Token bucket is the right strategy here (fixed-window had no burst, which is what made 10/min too strict). Comment rewritten to state the real burst/sustained semantics and that per-account lockout is the primary brute-force defence. Web builds clean (0 errors).

### L-02 — Host panel comments are stale ✅ ALREADY RESOLVED
- **Where:** `_HostPanel.cshtml`, `host-panel.js`.
- **Resolution (John):** stale TODO comments already cleaned up in a pass over the codebase's TODO comments. No further action.

### L-03 — Setup creation still has SVG QR path ✅ FIX IMPLEMENTED (removed)
- **Where:** `UltimateMonopoly/Services/Games/GameSetupService.cs` (`TryCreateNewGame`).
- **Resolution (John):** the QR returned from create-game was redundant dead code — it was instantly lost to the post-create redirect and never rendered. Removed entirely rather than standardised.

### L-04 — Board/stat display always uses default board ✅ FIX IMPLEMENTED
- **Premise correction (John):** board skins **only rename spaces** (card spaces excepted) — they do **not** change layout, sets, or prices. The `game-engine.md` doc wrongly said skins define "prices"; corrected in this pass. So the `StatisticsJob` *computation* against the default board is numerically correct (price-derived stats are identical across skins; `MostLandedOnBoardIndex` is an index). The only real concern was **display names**.
- **Fix (John — across all 5 stat pages):** each page now resolves board-index space names against the appropriate board:
  - **Per-game pages** — `Games/GameStats`, `Games/Compare` — use the **game's** board (`GetAllBoards().First(b => b.BoardId == game.BoardId)`, falling back to default).
  - **Cross-game / profile-aggregate pages** — `Profile/Index`, `Friends/Profile`, `Leaderboard/Compare` — use the **default** board (the only sensible choice for an aggregate spanning games that may have used different skins).
- No engine/`StatisticsJob` change needed; the projection stays board-canonical and the render layer picks the right names.

### L-05 — `ShortfallAmount`/`AmountOwed` computed properties rely on caller invariant ✅ FIX IMPLEMENTED
- **Where:** `PlayerBankruptedReceipt.ShortfallAmount`; `ShortfallPrompt.AmountOwed`.
- **Evidence (confirmed):** unsigned subtraction (`BankruptAmountBy - PlayerBalance`, `Cost - PlayerBalance`) assumed the debt exceeds the balance — wraps to a huge `uint` otherwise.
- **Fix:** clamp by comparing *before* the subtraction (`Math.Max` can't undo a wrap that already happened in the `uint` subtraction): `ShortfallAmount` → `BankruptAmountBy is { } amt ? (amt > PlayerBalance ? amt - PlayerBalance : 0) : null`; `AmountOwed` → `Cost > PlayerBalance ? Cost - PlayerBalance : 0`. Engine builds clean (0 errors).

## Positive findings / things that are now solid
- Per-game single-writer executor is the right shape for this app. It prevents multiple concurrent mutations to the working game model.
- Prompt path is correctly out-of-band from the pump, avoiding the classic deadlock where a prompt response queues behind the parked command waiting for that same response.
- Dice trigger cards are wired in current code: `DiceService.RollTurnDice` calls `OnSnakeEyes`, `OnRollDouble`, `OnRollTriple`, and `OnOtherRollsTriple` before emitting `DiceRollReceipt`.
- Card stats migration now appears present (`20260619125257_CardStats.*`); older session-note debt is resolved.
- Card money actions route player-to-player collections from the payer POV for `EachPlayer`, `TriggerPlayer`, and `DiceOffPlayer`, so counterparty shortfalls are handled correctly in those paths.
- Transaction service keeps `SaveChanges` out of mid-turn service calls; commit-at-boundary design is correct for working-copy integrity.
- Game cancellation tears down runtime after DB update; completion should copy that ordering.

## Recommended fix order
1. **Before release:** H-01, H-02, H-03, H-04, M-01.
2. **Next pass:** M-02, M-03, M-04.
3. **Stats polish:** M-05, M-06, M-07, L-04.
4. **Maintainability:** M-08, M-09, M-10, L-01, L-02, L-03, L-05.

## User-reported runtime bugs (2026-06-22, John — found in live play)
These are gameplay bugs reported from play, not static-analysis findings. Recorded verbatim-in-intent for diagnosis; root causes not yet confirmed.

### R-01 — Prompt requiring more selections than available options locks the game ✅ FIX IMPLEMENTED
- **Symptom:** a prompt whose required count exceeds the number of available options (e.g. needs 2, only 1 option exists) cannot be submitted — the UI expects 2 selections but only 1 can be selected, so the prompt never satisfies and the game hangs.
- **Root cause:** `TargetPropertyPrompt`/`TargetPlayerPrompt` carry a caller-fixed `Count`, enforced as *exactly* `Count` by both the client submit gate (`ingame-prompts.js`) and `PromptValidator` (`!= Count`). `PurgingService.PurgeProperty` opened the prompt with `Count = propCount` (2 for the "purge 2" cards) unclamped; when the owner had only 1 eligible (un-purged, built-on) property the prompt was unsatisfiable at both ends → game parked. Sibling sites (`PropertyActionService.ChooseProperties`/`TakeFromBank`) already clamped with `Math.Min`; purging was the one variable-count site that didn't.
- **Fix (four layers, defence-in-depth):**
  1. **Framework backstop** — `TargetPropertyPrompt.Count`/`TargetPlayerPrompt.Count` are now computed properties clamped to `Math.Min(_count, eligible.Count)`. Single source of truth that flows to both the razor partial (`data-target-count`) and the validator, so no call site can ever construct an unsatisfiable prompt.
  2. **Server validator** — `ValidateTargetProperty`/`ValidateTargetPlayer` require `Math.Min(prompt.Count, eligible.Count)` selections (not a literal `== Count || == eligible.Count`, which would also wrongly accept selecting *all* options when only a subset was wanted).
  3. **Client gate** — `ingame-prompts.js` (TargetProperty + TargetPlayer) clamps to `Math.min(count, available)` against the rendered option count; mirrors the validator.
  4. **Call-site clamp + rewording** — `PurgingService.PurgeProperty` now derives `pick = Math.Min(propCount, eligibleProps.Count)` and uses it for `Count`, the title/body, and the acknowledge text (the backstop clamps `Count`, but the wording is built at the call site so it must match the clamped number).
- **Scope checked:** all 11 `TargetPropertyPrompt`/`TargetPlayerPrompt` sites — lock-safety is now universal via the backstop; `PurgingService` was the only one with count-dependent wording needing the reword. No other prompt type has a "select exactly N of M where N can exceed M" pattern. Engine builds clean (0 errors).
- **Note:** independent of R-03 (re-purge) — that grows the eligible set but doesn't remove the need for the clamp.

### R-02 — Cards do not get played IN jail ✅ FIX IMPLEMENTED
- **Symptom:** held cards are not offered/played while the holder is in jail. Some cards genuinely **need** to fire in jail (literal trigger — e.g. the dodgy-judge "double→triple only when in jail" card).
- **Root cause:** `CardTriggerService.MatchingCardForTrigger` carried a blanket player-level exclusion `where !player.IsInJail || trigger == CardTrigger.OnInJail` (added in the 18th-S2 session). It dropped a jailed holder from *every* trigger except `OnInJail` **before** the per-card conditions were evaluated, killing the dodgy-judge card (`Trigger=OnRollDouble` + condition `JailFilter=OnlyJailed`) even though its trigger does fire for a jailed roller (`DiceService.RollTurnDice` calls `OnRollDouble` regardless of jail state).
- **Decision (John):** make **all** cards playable in jail — remove the blanket exclusion entirely rather than add a narrower carve-out. This reverses the 18th-S2 "exclude jailed holders from all triggers except `OnInJail`" decision.
- **Fix:** deleted the jail `where` clause in `MatchingCardForTrigger`. The per-condition `JailFilter` switch (`OnlyJailed`/`OnlyNotJailed`) stays and still gates genuinely jail-specific cards.
- **Verified safe:**
  - **GOOJF cards do not double-fire** — all 4 decks (`chance`/`comChest`/`percentChance`/`percentComChest`) author the Get-out-of-jail-free card with `Conditions: [{ "Trigger": 0 }]` (`CardTrigger.None`), so `None.HasFlag(realTrigger)` is false → they never surface through the trigger pipeline. They play only via their own `LeaveJailCard` command path (`JailAction{Kind=Release}`). `ConditionType=ChoiceCardholderTurn` is kept only so the profile cards tab lists them.
  - **No movement/advance jail-escape exploit** — anytime own-turn cards fire on `OnTurnStart`/`OnSpaceLand`; `OnTurnStart`'s play-a-card command is gated `!IsJailed` in `TurnStateProvider.CanPortfolioCommand`, and `OnSpaceLand` requires the jailed player to move. Neither fires in jail, so a jailed player still can't advance out. `CanPortfolioCommand`'s `!IsJailed` gate was left intact (John's call).
- **Net effect:** dodgy-judge double→triple now works in jail; jailed players may now react as bystanders to `OnOther*` triggers (former-prisoner GO steal, FP take, cancel triple bonus). Engine builds clean (0 errors).
- **Linkage:** the downstream handling of a double *converted to a triple while jailed* runs through the orchestrator's modified-roll path — see R-08.

### R-03 — Purging should allow purging the same property AGAIN ✅ FIX IMPLEMENTED
- **Change requested:** rework purging so a property that has already been purged can be purged again.
- **Root cause:** `PurgingService.PurgeProperty` filtered the eligible set with `.Where(p => !p.HasBeenPurged)`. `HasBeenPurged` was a permanent historical flag (only reset on ownership change), so once a property was purged it could never be purged again — even after being rebuilt.
- **Fix:** removed the `!HasBeenPurged` filter. `BuiltOnProperties` already requires current buildings (`BuiltOn()` = `RentLevel.ONE_HOUSE..DOUBLE_HOTEL`), so a property only re-enters the eligible set once it has been rebuilt after a prior purge — exactly "purge the same property again". A property currently sitting at `SET` (purged, not yet rebuilt) is still excluded because it has nothing to strip.
- **`HasBeenPurged` removed entirely:** the flag is now gone — its assignment in `PurgeProperties`, the `PropertyModel` field/copy-ctor/`OwnProperty` reset, and the profile "Purged Before" badge (`_PlayerProfileView.cshtml`) were all removed. `IsPurged` (currently-purged, "Purged" badge) is the only purge-state flag now. Old snapshots that still carry `HasBeenPurged` deserialise fine (unknown JSON property ignored). Builds clean (0 errors).
- **Note:** does not touch the `SwapSet` direct-purge path — see R-05 (purging a 0-building set still wrongly emits a purge).

### R-04 — `NormaliseProperties` not run for multi-card resolutions → wrong rent level ✅ FIX IMPLEMENTED
- **Symptom:** `NormaliseProperties` is not invoked when cards resolve, so in edge cases properties keep a `SINGLE` rent level when they should be `SET`. Later "build on set" logic expects `SET` and errors.
- **Root cause:** `CardService.ResolveCard` applies a card's actions (`foreach … ApplyAction`) but never re-normalises. Property-moving card actions (`ReturnToBank`, `HandInToFreeParking`, `TakeFromBank`, `ReceiveAllFreeParking`, `ClearFreeParkingToBank`) change ownership without re-normalising rent levels — only `SwapSet` and `GrantHotel` happened to call it internally. A card completing/breaking a set left properties at the wrong `RentLevel`.
- **Fix:** inject `PropertyService` into `CardService` and call `NormaliseProperties(engine)` once at the end of `ResolveCard`, after the action loop. This is the single chokepoint every card funnels through — resolve-on-draw (`DrawCard`) and held plays (`PlayCard`), including the multiple cards resolved in a trigger chain (each calls `ResolveCard`). So every property-affecting and multi-action card re-normalises uniformly.
- **DI:** no cycle — `PropertyService`'s dependency subtree (`Auction`/`Transaction`/`PropertyTransfer`/`CardTrigger`) never constructor-injects `CardService` (it's reached via the engine bundle). `CardService` already depended on `PropertyService` transitively through the action services. Engine builds clean (0 errors).
- **Note:** `SwapSet`/`GrantHotel` keep their internal `NormaliseProperties` calls (harmless — the recompute is idempotent); left for minimal churn.
- **Tests:** the engine suite has 17 pre-existing `TransactionService_Tests` failures unrelated to these changes (TransactionService untouched); noted, not addressed here.

### R-05 — 0-house set that is swapped and purged marks houses as purged when there was nothing to purge ✅ FIX IMPLEMENTED
- **Symptom:** swapping then purging a set with 0 houses records a purge even though nothing was purged.
- **Root cause:** `PropertyActionService.SwapSet` purges the whole swapped set (`PurgeProperties(target, holderIndexes)` / `(holder, targetIndexes)`) regardless of buildings, and `PurgingService.PurgeProperties` unconditionally set `IsPurged = true`, reset `RentLevel = SET`, and emitted a `PropertyPurgedReceipt` for every index. For a 0-building set this wrongly flagged `IsPurged` (which blocks the set from being built on under the even-building rule) and emitted spurious purge receipts/stats.
- **Fix:** guard `PurgeProperties` to skip any property that is not `BuiltOn()` (`RentLevel < ONE_HOUSE`) — nothing to strip. Placed centrally in `PurgeProperties` so it also protects future callers; the card-purge path (`PurgeProperty`) is unaffected because it only ever passes built-on properties from `BuiltOnProperties`. A 0-building set still *transfers* in `SwapSet`; it just isn't flagged purged, and `NormaliseProperties` sets the complete set to `SET` as normal. Engine builds clean (0 errors).

### R-06 — Free-hotel tax card does nothing ✅ FIX IMPLEMENTED
- **Symptom:** the tax card "pay tax, receive a free hotel (if available)" did not actually grant a hotel.
- **Root cause:** `BuildingActionService.GrantHotel` only acted when the player *already* owned a complete street built to exactly four houses at the instant of landing (`RentLevel == FOUR_HOUSES`), then bumped it to `HOTEL`. That state is almost never true on a tax landing, so the card silently no-opped.
- **Decision (John):** the card should always give something — place a hotel now if possible, otherwise grant a held credit.
- **Fix (3 cases + consumption + UI):**
  1. **`PlayerModel.FreeHotels`** (`ushort`) — new held-credit counter (+ copy ctor).
  2. **`GrantHotel`:** if a hotel is in the pool **and** the player has a four-house street → place it now (current behaviour, holder picks which). Otherwise (no four-house street, **or** the pool is empty) → `FreeHotels++` with an acknowledge. Never a silent no-op.
  3. **Consumption (`BuildingService`):** in the core `BuildOnProperties` charge loop, a hotel step (`FOUR_HOUSES → HOTEL`) is free while `FreeHotels > 0` — one credit per hotel, decremented there (the single source of truth; double-hotel builds are not covered). A read-only `FreeHotelDiscount` helper mirrors that waiver in all three build entry points (single / set / all) so the affordability check and confirmation prompt show the real discounted cost (otherwise a cash-poor player holding a credit would be falsely blocked).
  4. **UI:** "Free hotels" row on the property tab of the player profile (`_PlayerProfileView.cshtml`), shown when held.
- **Interpretation note:** John's spec said "decrease total build cost by build cost × FreeHotels"; implemented as **one credit waives one hotel build** (capped at the number of hotels actually built), since a literal `cost × credits` would over-discount a single hotel build and could go negative.
- Engine + web build clean (0 errors).

### R-07 — "Each player hands in a property" FP card lets the landing player hand in one of the just-transferred properties ✅ FIX IMPLEMENTED
- **Symptom:** the FP card where each other player hands a property into Free Parking and the holder receives them — because the properties transferred to the holder **before** the FP method resolved, the holder could then hand one of those just-received properties back in.
- **Root cause:** the card carried two actions — `Property{HandInToFreeParking, Target=AllOthers}` **and** `Property{ReceiveAllFreeParking}`. The explicit `ReceiveAllFreeParking` moved the handed-in properties to the holder *before* `ProcessFreeParking` computed the default hand-in eligibility (`TradableProperties`, `:105`), so the just-received properties were eligible for the path-B hand-in (`:141`).
- **Fix (John's call — option B, data-only, no code change):** removed the `ReceiveAllFreeParking` action from the card (`freeParking.json`). The holder now receives the properties via the **default FP property-take** (`TakeFromFreeParking`, `:116`), which runs *after* eligibility is computed (`:105`), so a just-received property is never offered back. R-07 closed with no engine change.
- **Accepted trade-off:** the default take is gated behind the FP money path — when the pot has no cash, path A (`:74`) pays the dice-difference fee and the properties are **not** received. Rather than rework path A (a broader deviation from `game-rules.md` FP rule 2), the card text now reads "...you will receive all properties in free parking **(if free parking has money)**". `cards.md` updated to match.
- **Note:** card JSON change → takes effect on a **freshly created game** only (decks shuffle in at creation); existing games keep the old two-action card.

### R-08 — Player-turn orchestrator + dice rolls need real work (upgrade/downgrade, steal/cancel triple bonus) ⏸️ DEFERRED / NEEDS MORE TESTING
- **Symptom (as originally noted):** suspected quirks around modified dice — convert/upgrade double→triple, downgrade triple→double, steal triple bonus, cancel triple bonus.
- **Status:** **Not a confirmed bug.** This was flagged on the strength of gameplay "vibes" plus one or two "hmm, should that have happened?" moments across a couple of real games — not a reproduced defect. A read-through of `PlayerTurnOrchestrator` (the roll-type switch, `ModifiedDiceRollType` re-routing, the triple-bonus accumulator-vs-payout split, and the doubles/triples-in-a-row counter ordering) did **not** surface an obvious fault; the sequencing looks correct.
- **What it needs before any change:** proper verification rather than play-testing by feel —
  - **Unit tests** over the orchestrator's roll-resolution paths: Normal / Double / Double→Triple (convert) / Triple / Triple→Double (downgrade), including the doubles/triples-to-jail thresholds and the extra-roll decision.
  - **Simulated card scenarios**: convert-double-to-triple, downgrade-triple-to-double, "do not receive triple bonus", cancel-a-player's-triple-bonus, steal-triple-bonus-to-lowest-roller — asserting the accumulator (+£500) vs payout split and the suppress-default interaction at each point.
  - The in-jail converted-roll path from **R-02** (a double→triple played while jailed) should be covered here too.
- **Action:** left in place; revisit once the test scaffolding exists so any fix is driven by a reproduced failing test, not impressions. No code touched.

## Tests to add
- Completion failure test: simulate DB save failure; runtime must remain available until commit succeeds.
- Board skin edit/create/share invalidates current user's cached board list.
- Movement stats with no landings does not throw.
- CardOptionPrompt play-card validation: empty allowed, valid option allowed, invalid key rejected.
- Immunity prompt only plays when selected key equals the immunity card ID.
- Concurrent `GameCacheService.GetGame` cache-miss requests return the same instance.
- Non-current deal proposer behavior test matching final rule decision.
- Card import missing file fails loudly.
- Jail card exit stats and card dice-roll stats once receipt model is decided.
