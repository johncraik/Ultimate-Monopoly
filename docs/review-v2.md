# Ultimate Monopoly dev deep review — 2026-06-22

Source reviewed: `Ultimate-Monopoly-dev.zip`  
Repo docs reviewed: `docs/review.md`  
Review type: static source review only. The sandbox has no .NET SDK, so this was **not** build-verified or test-run-verified.

## Scope actually covered

- Unpacked repo and read the updated `docs/review.md` first.
- Inspected the previous high-risk fix areas directly.
- Broadened into game engine flow, cards, dice, prompts, turn state, snapshots, cache/executor, stats, board skins/sharing, imports, DI, migrations, tests, and config JSON.
- Parsed all card JSON files successfully.
- Counted roughly 626 files total, about 374 `.cs` files, and 189 xUnit facts/theories in `MP.GameEngine.Tests`.

## Executive verdict

The previous review was mostly acted on properly. The big dangerous items are mostly fixed: game completion commit ordering, game cache single-flight hydration, movement stats empty guard, prompt validation for card options, card dice stats, jail-card stats, and many card trigger integrations.

However, there are still several real defects/risks. The most important one is the triple downgrade flow: a triple converted/downgraded to a double can still receive the triple bonus before the effective roll type is re-read.

## Fix verification matrix

| Old item | Status | Evidence / note |
|---|---:|---|
| H-01 game completion clears runtime before DB commit | Fixed | `GameCompletionService.ConcludeGame` commits DB before `ClearGameRuntime(gameCache.GameId)`. |
| H-02 board cache invalidation broken for normal users | Mostly fixed, one remaining share-removal bug | `BoardCacheService.Invalidate(userId, bypassAdminCheck)` exists and services call it. But removed share recipients are not invalidated. See `NEW-M01`. |
| H-03 movement stats `MaxBy` empty crash | Fixed | `MovementStatsService` checks `landOnIndexes.Count > 0`; falls back to GO. |
| H-04 card import silently skips missing files | Accepted by design | Still `continue`s on missing/empty files. Fine if deliberate, but not fail-fast. |
| M-01 card option prompt accepts arbitrary key | Fixed | `PromptValidator.ValidateCardOption` now requires empty key or key contained in options. `CardImmunityService` checks exact card ID. |
| M-02 cache hydration not single-flight | Fixed | `GameCacheService` uses per-game `SemaphoreSlim`, double-checks cache inside lock. |
| M-03 deal allowed at turn boundary | Partially fixed | `CanDeal` now allows any active player at turn boundary. But active-player check can throw for invalid IDs. See `NEW-M02`. |
| M-04 unbounded/unsafe game executor queue | Mostly fixed | Bounded channel and pump reclamation exist. But full queue silently drops accepted commands. See `NEW-M04`. |
| M-06 jail exit stats | Fixed | `JailStatsService` counts card releases via `CardPlayedReceipt.IsJailRelease`. |
| M-07 card dice roll stats | Fixed | `DiceService.RollCardDice` emits non-turn `DiceRollReceipt`; `MovementStatsService` separates card rolls. |
| R-04 property normalisation after cards | Fixed | `CardService.ResolveCard` calls `_propertyService.NormaliseProperties(engine)` after action resolution. |
| R-05 purge 0-house properties | Fixed | `PurgingService.PurgeProperties` skips `!property.BuiltOn()`. |
| R-06 free hotel tracking | Implemented | `PlayerModel.FreeHotels`, `BuildingActionService.GrantHotel`, and `BuildingService.FreeHotelDiscount` exist. Needs gameplay tests. |
| R-08 dice conversion/downgrade | Still risky | Triple downgrade still applies triple bonus before effective roll branch. See `NEW-H01`. |

## Remaining / new defects

### NEW-H01 — Triple downgraded to double still gets triple bonus

Severity: **High**  
Area: game engine / dice / card conversion  
Files:

- `MP.GameEngine/Services/PlayerTurnOrchestrator.cs`
- `MP.GameEngine/Services/SubSystems/DiceService.cs`
- relevant docs: `docs/development/design-docs/cards/cards.md`, `cards-dev-changes.md`, `game-rules.md`

Evidence:

`PlayerTurnOrchestrator.TripleRoll` draws the triple card, then immediately resolves the default triple bonus, then re-reads the effective dice roll type.

Current flow:

```csharp
var suppressDefault = await engine.CardService.DrawCard(engine, player, CardType.Triple, ct);
sd.Aggregate(suppressDefault);
if (!sd.SuppressTripleBonus)
{
    await _playerService.ResolveTripleBonus(engine, player, ct);
}

dice = engine.Cache.GetTurnDiceRoll() ?? throw ...;
switch (dice.RollType)
{
    case DiceRollType.Double:
        await HandleDoubleRoll(...);
        return true;
    ...
}
```

Problem:

If a card changes the roll from triple to double, the player still receives/increments the triple bonus before the double branch runs. The docs say dice conversion/downgrade applies before counters/default handling. Effective roll should drive default handling.

Fix:

Move default triple bonus resolution into the final effective-triple branch only. Re-read `engine.Cache.GetTurnDiceRoll()` before resolving triple-only defaults. If effective roll is double, do not pay triple bonus unless the card explicitly paid a custom bonus.

Test required:

- Roll triple.
- Held/triggered card downgrades triple to double.
- Assert no default triple bonus paid/incremented.
- Assert double handling/counters/extra-turn behaviour is applied instead.

---

### NEW-H02 — Dice roll receipt records original roll, not effective roll after card conversion

Severity: **High/Medium**  
Area: stats / history / event receipts  
File: `MP.GameEngine/Services/SubSystems/DiceService.cs`

Evidence:

`RollTurnDice` emits the receipt using the original `roll` object after triggers may have mutated `GameModel.ModifiedDiceRollType`.

```csharp
engine.EventEmitter.Emit(new DiceRollReceipt(player.PlayerId, roll));
return (engine.Cache.GetTurnDiceRoll() ?? throw ..., sd);
```

Problem:

The returned dice can be converted/downgraded, but the persisted event receipt still says the original roll. That pollutes:

- turn event history;
- movement/dice stats;
- double/triple counts;
- any future replay/debugging based on receipts.

Fix options:

1. Emit the effective roll:

```csharp
var effective = engine.Cache.GetTurnDiceRoll() ?? throw ...;
engine.EventEmitter.Emit(new DiceRollReceipt(player.PlayerId, effective));
return (effective, sd);
```

2. Better long-term: receipt stores both `OriginalRoll` and `EffectiveRoll`, because this app has card-driven roll conversion and that is useful audit evidence.

Test required:

- Roll triple, card downgrades to double.
- Assert receipt says double/effective or stores both original triple and effective double.

---

### NEW-M01 — Board share removal does not invalidate removed users' board cache

Severity: **Medium**  
Area: board skins / sharing / cache invalidation  
File: `UltimateMonopoly/Services/BoardSkins/BoardSkinShareService.cs`

Evidence:

`TryShareBoardSkin` computes removed shares:

```csharp
var toDelete = existingLinks.Where(sbs => !sbs.IsDeleted && !userIds.Contains(sbs.UserId)).ToList();
```

But after commit it only invalidates current `userIds`:

```csharp
foreach (var userId in userIds)
{
    _boardCacheService.Invalidate(userId, true);
}
```

Problem:

Users removed from a share can keep stale cached board-skin data until cache expiry. Actual join validation may still be DB-backed, but the board list/dropdown can remain wrong.

Fix:

Invalidate all affected users after commit:

```csharp
var affectedUserIds = userIds
    .Concat(toDelete.Select(x => x.UserId))
    .Concat(toRestore.Select(x => x.UserId))
    .Concat(toAdd.Select(x => x.UserId))
    .Distinct()
    .ToList();
```

Then invalidate each with bypass.

Test required:

- Share board with user A.
- Prime user A board cache.
- Remove user A from share.
- Assert user A cache/list no longer includes the board immediately.

---

### NEW-M02 — `CanDeal` / `CanDeclareBankruptcy` can throw for invalid player IDs

Severity: **Medium**  
Area: SignalR command gating / turn-state provider  
File: `MP.GameEngine/Services/Framework/TurnStateProvider.cs`

Evidence:

```csharp
private bool IsActivePlayer(string playerId) =>
    cache.Game.GetPlayer(playerId) != null;
```

`GameModel.GetPlayer` throws when the player is not found. Therefore a capability method that should return `false` can throw instead.

Problem:

Client-submitted player IDs are untrusted/stale-prone. `CanDeal` and `CanDeclareBankruptcy` are public command gates and should be safe no-op checks, not exception paths.

Fix:

Replace with a non-throwing lookup:

```csharp
private bool IsActivePlayer(string playerId) =>
    cache.Game.Players.Any(p => p.PlayerId == playerId /* && !p.IsBankrupt if that flag exists */);
```

Or add `TryGetPlayer` to `GameModel`.

Test required:

- Call each public `Can...` method with a bogus player ID.
- Assert it returns false and does not throw.

---

### NEW-M03 — Snapshot and turn-event snapshot are not atomic

Severity: **Medium/High**  
Area: persistence / recovery / stats/history integrity  
Files:

- `UltimateMonopoly/Services/GameEngine/SnapshotService.cs`
- `MP.GameEngine/Services/Framework/TurnStateProvider.cs`

Evidence:

Transitions do this:

```csharp
await snapshotService.CreateSnapshotAsync(cache.Game);
await snapshotService.CreateTurnEventSnapshotAsync(cache.GameId, turnId, cache.Events.ToList());
```

`CreateSnapshotAsync` commits its own transaction before `CreateTurnEventSnapshotAsync` writes events.

Problem:

If event snapshot persistence fails after the game snapshot commits, the game advances but the turn receipts are lost. On recovery, the state resumes from the advanced snapshot, but stats/history for the previous turn can be incomplete.

Fix:

Persist `GameTurn`, `GameSnapshot`, and `GameTurnEvents` in one transaction, ideally one service method:

```csharp
CreateSnapshotWithEventsAsync(game, previousTurnId, receipts, finalTurn)
```

Test required:

- Simulate failure after snapshot insert before event insert.
- Assert transaction rolls back everything, or state/event persistence is otherwise consistent.

---

### NEW-M04 — Full game command queue silently drops commands after caller accepted them

Severity: **Medium**  
Area: SignalR / game executor / UX consistency  
File: `UltimateMonopoly/Services/GameEngine/GameExecutor.cs`

Evidence:

`GamePump` uses bounded channel capacity 10 and `TryWrite`. If full, `TryEnqueue` returns `Full`.

The current executor design logs/drops on full rather than giving the original caller a command failure result. Pre-checks in hubs can already have said the command is allowed, but enqueue can still fail under load/backpressure.

Problem:

A valid player/host action can disappear with no UI-level failure. This is better than unbounded memory growth, but not ideal for a tabletop controller app where users need deterministic feedback.

Fix options:

- Return enqueue outcome to hub and send a clear “game busy / retry” response.
- Or use `WriteAsync` with a short timeout/cancellation and return failure if not accepted.
- Keep bounded capacity.

Test required:

- Fill queue.
- Submit one more command.
- Assert caller receives explicit failure/retry signal, not silent drop.

---

### NEW-M05 — Stats job is idempotent by PK, but still worth guarding/logging

Severity: **Low/Medium**  
Area: stats projection / Hangfire  
Files:

- `UltimateMonopoly/Services/Statistics/GameStatsService.cs`
- `UltimateMonopoly/Services/Statistics/StatisticsJob.cs`
- `UltimateMonopoly/Data/Migrations/20260610093126_PlayerGameStat.cs`

Evidence:

`PlayerGameStats` primary key is `{ GameId, UserId }`, so duplicate rows are DB-blocked.

Problem:

Concurrent stats jobs can still race: both read “missing”, both compute, one insert succeeds, the other gets a PK failure unless handled by repository/EF flow. This is not data corruption, but can create noisy failed jobs.

Fix:

Either:

- make the job game-specific and enqueue one job per game;
- or catch duplicate-key conflicts and treat them as “already computed”; 
- or use an upsert/merge pattern.

Test required:

- Run two stats jobs concurrently against same completed game.
- Assert no final duplicate rows and no failed/retried job noise.

## Additional observations

### Card JSON parse check

All card config files parsed as valid JSON during this review:

- `chance`: 16
- `comChest`: 16
- `double`: 10
- `freeParking`: 10
- `go`: 10
- `goToJail`: 10
- `justVisiting`: 10
- `percentChance`: 16
- `percentComChest`: 16
- `tax`: 10
- `third`: 35
- `triple`: 10

This only proves syntactic JSON validity, not semantic correctness.

### Tests are concentrated in engine tests

`MP.GameEngine.Tests` has meaningful coverage, but the remaining risks are integration-style issues:

- SignalR command enqueue feedback;
- board share cache invalidation;
- snapshot + event atomicity;
- stats job concurrency;
- full dice/card conversion flow.

These need targeted tests, not just more unit tests around isolated services.

### Build status not verified

No `.NET SDK` is installed in the review sandbox, so this review cannot claim:

- solution builds;
- tests pass;
- migrations apply;
- nullable warnings are clean;
- runtime DI graph resolves.

Run locally/CI:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

## Recommended next work order

1. Fix `NEW-H01` triple downgrade bonus ordering.
2. Fix `NEW-H02` effective dice receipt/audit mismatch.
3. Fix `NEW-M01` removed-share cache invalidation.
4. Fix `NEW-M02` non-throwing active-player checks.
5. Decide whether `NEW-M03` needs atomic snapshot/event persistence before release. I would fix it before public release.
6. Add direct regression tests for all five above.
7. Improve executor full-queue caller feedback.
8. Add stats job concurrency guard if Hangfire retries show duplicate-key noise.

## Claude-ready bug list

### BUG: Triple downgrade still pays triple bonus

- Severity: High
- File: `MP.GameEngine/Services/PlayerTurnOrchestrator.cs`
- Method: `TripleRoll`
- Problem: default triple bonus is paid before effective dice type is re-read.
- Impact: triple downgraded to double can still get triple bonus.
- Fix: re-read effective roll before triple defaults; only pay triple bonus in effective-triple branch.

### BUG: Dice receipt emits original roll, not effective roll

- Severity: High/Medium
- File: `MP.GameEngine/Services/SubSystems/DiceService.cs`
- Method: `RollTurnDice`
- Problem: `DiceRollReceipt` uses `roll`, while returned value uses `engine.Cache.GetTurnDiceRoll()`.
- Impact: stats/history can record triple when actual effective roll became double.
- Fix: emit effective roll or store original+effective in receipt.

### BUG: Removed board-skin share recipients keep stale cache

- Severity: Medium
- File: `UltimateMonopoly/Services/BoardSkins/BoardSkinShareService.cs`
- Method: `TryShareBoardSkin`
- Problem: invalidates `userIds` only, not `toDelete` users.
- Impact: removed users may still see shared board in cached board list.
- Fix: invalidate all affected user IDs: added, restored, retained, removed.

### BUG: Capability checks throw on bogus player ID

- Severity: Medium
- File: `MP.GameEngine/Services/Framework/TurnStateProvider.cs`
- Method: `IsActivePlayer`
- Problem: uses throwing `cache.Game.GetPlayer(playerId)`.
- Impact: malformed/stale client command can throw instead of returning false.
- Fix: use non-throwing `Players.Any(...)` or `TryGetPlayer`.

### BUG/RISK: Snapshot and turn events are persisted separately

- Severity: Medium/High
- Files: `SnapshotService.cs`, `TurnStateProvider.cs`
- Problem: snapshot commits before event snapshot write.
- Impact: game can advance without matching turn event receipts.
- Fix: persist snapshot + events in one transaction/method.

### BUG/RISK: Full queue drops command without caller feedback

- Severity: Medium
- File: `UltimateMonopoly/Services/GameEngine/GameExecutor.cs`
- Problem: bounded channel full returns `Full`; caller path does not surface failure to UI.
- Impact: valid host/player command can disappear under backpressure.
- Fix: return enqueue status to SignalR caller; show retry/busy response.

### RISK: Stats jobs can race and hit PK duplicate

- Severity: Low/Medium
- Files: `StatisticsJob.cs`, `PlayerGameStat` migration
- Problem: PK prevents duplicate rows, but concurrent jobs can both compute missing stats and one can fail insert.
- Impact: Hangfire retry/noise, not data corruption.
- Fix: game-specific job, duplicate-key swallow, or upsert.
