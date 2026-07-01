# Ultimate Monopoly Audit Issues

Source: `Ultimate-Monopoly-master.zip`  
Audit scope: full uploaded codebase review, every file read.  
Build/test status: not run, per instruction.  
Classification basis: follow-up triage with confirmed major/minor decisions.

This document excludes:
- non-issues / intended behaviour
- ignored items
- explicitly deferred V2 features

## Major Issues

### UM-001 — Triple downgrade still pays the triple bonus

**Classification:** ~~Major~~ Not a bug (resolved)  
**Type:** Gameplay / card-effect economy bug  
**Status:** Not a bug — closed after investigation

#### Resolution

Investigated and confirmed **not a real payout bug**. The only triple-downgrade effect
("Your next triple is downgraded to a double", `config/cards/third.json:581`) is a held
Third-deck card on the `OnRollTriple` trigger, which fires inside
`DiceService.RollTurnDice` **before** the orchestrator's roll-type switch. It sets
`ModifiedDiceRollType = Double`, so `GetTurnDiceRoll()` already returns `Double` and the turn
routes through the top-level `Double` branch (`PlayerTurnOrchestrator.cs:107`) — `TripleRoll`
is never entered for a downgraded roll, so the bonus is never reached. The downgrade avoids
the bonus by path routing, not via `SuppressTripleBonus`.

The one real defect was a **redundant dice re-fetch** inside `TripleRoll`: it re-read the roll
from cache despite the up-to-date roll already being passed in as a parameter. Nothing between
capture and re-fetch can change `ModifiedDiceRollType` (no Triple-deck card downgrades, and
`DrawCard` fires no roll triggers), so the re-fetch could only ever return the same value.
Removed it — behaviourally a no-op cleanup. Fixed in `PlayerTurnOrchestrator.cs`.

#### Original summary (superseded)

A triple-downgrade effect can still leave the player receiving the triple bonus payout. The result is economically wrong: the action is meant to apply the downgrade/penalty-style effect, but the reward multiplier logic still treats the triple condition as payable.

#### Why this matters

This directly affects game balance. Unlike a display-only issue, this changes money movement and therefore can affect the winner, player solvency, trade leverage, bankruptcy pressure, and the final state of the game.

Because card effects are a core gameplay path, this should be treated as a next bug-fix push item.

#### Likely impact

- Player can receive more money than they should.
- Card/effect behaviour becomes inconsistent with the intended rule.
- Game outcomes can be skewed.
- Debugging later stats/replays becomes harder because the persisted game state contains the wrong money outcome.

#### Expected fix direction

Review the card/effect execution flow where downgrade and triple-bonus logic interact.

The important fix is not just to patch the visible payout, but to make the command/effect state explicit:

- decide whether downgrade cancels triple bonus,
- ensure the bonus calculation checks the post-effect eligibility state,
- ensure any receipt/event generated records the actual final amount,
- add a focused regression case around triple downgrade + payout calculation.

#### Suggested regression test

Create a deterministic card/effect scenario where:

1. Player is eligible for triple-related processing.
2. Triple downgrade is applied.
3. The payout calculation runs.
4. Assert the player does **not** receive the triple bonus if the downgrade should cancel it.
5. Assert receipts/events match the actual amount received.

---

### UM-005 — Board-skin cache invalidation misses shared users and removed-share users

**Classification:** Major (impact closer to Minor — see resolution)  
**Type:** Cache invalidation / shared-board consistency bug  
**Status:** Fixed

#### Resolution

Fixed in `BoardSkinService.cs` and `BoardSkinShareService.cs`.

- **Problem A** (edits to a shared board only invalidated the owner) and **Problem B**
  (deletes only invalidated the owner): added `InvalidateSkinCachesAsync(boardSkinId)` /
  `InvalidateSkinCaches(recipients)` helpers that clear the owner cache plus every active
  share recipient. Wired into `TryUpdateBoardSkin` and the three space methods; `TryDeleteBoardSkin`
  invalidates from the `shareLinks` captured *before* the soft-delete.
- **Problem C** (removed recipients never invalidated): `TryShareBoardSkin` now invalidates the
  distinct union of `toAdd` + `toRestore` + `toDelete` + submitted `userIds`.
- **Problem D** dropped — the owner-side share view is served by direct DB queries, not this cache,
  so there is nothing stale to invalidate.

Recipient invalidations pass `bypassAdminCheck: true` (the acting owner is not a SystemAdmin).

**Impact note:** realistically closer to Minor than Major. The game-setup selection dropdown and
the in-play board loader (`GetGameBoard`, `activeShareOnly: false`) both hit the DB directly, so the
stale cache never corrupts a running game — it only affects board name/details display and the
`TryStartGame` validation list, for up to the 6-hour cache window.

#### Main files involved

- `UltimateMonopoly/Services/BoardSkins/BoardSkinService.cs`
- `UltimateMonopoly/Services/BoardSkins/BoardSkinShareService.cs`
- `UltimateMonopoly/Services/Cache/BoardCacheService.cs`
- `UltimateMonopoly/Services/Imports/BoardImportService.cs`

#### Summary

Custom board skins are cached per user. A user’s cached board list includes both boards they own and boards shared with them.

The cache key shape is user-specific:

```csharp
private string GetKey(bool isDefault, string? userId = null)
    => $"{CacheKey}__{(isDefault ? DefaultBoardKey : userId ?? _userInfo.UserId)}";
```

The custom-board query includes shared boards:

```csharp
.Where(b => b.UserId == userId 
    || b.SharedWith.Any(sbs => !sbs.IsDeleted && sbs.UserId == userId))
```

That means when an owner edits, deletes, shares, or unshares a board, the owner’s cache is not the only cache that can become stale.

#### Problem A — editing an already-shared board only invalidates the owner

`BoardSkinService.TryUpdateBoardSkin()` calls:

```csharp
_boardCacheService.Invalidate();
```

with no user ID. That only clears the current user’s board cache.

If Alice shares Board A with Bob:

1. Bob opens game setup.
2. Bob’s custom board list is cached.
3. Alice edits Board A.
4. Only Alice’s cache is invalidated.
5. Bob can keep seeing stale board data until expiry.

The same pattern appears around board space edits/adds/deletes.

#### Problem B — deleting a board only invalidates the owner

`TryDeleteBoardSkin()` soft-deletes the board and share links, then calls:

```csharp
_boardCacheService.Invalidate();
```

Again, this only invalidates the current user/owner cache.

Shared users can keep the deleted board in their cached selectable list until expiry. Deeper validation may block actually starting with it, but the UI/data exposed to the user is stale.

#### Problem C — removed share recipients are not invalidated

`BoardSkinShareService.TryShareBoardSkin()` invalidates the submitted/current user IDs:

```csharp
foreach (var userId in userIds)
{
    _boardCacheService.Invalidate(userId, true);
}
```

But removed users are calculated separately:

```csharp
var toDelete = existingLinks
    .Where(sbs => !sbs.IsDeleted && !userIds.Contains(sbs.UserId))
    .ToList();
```

Those removed users are not in `userIds` anymore, so their cache is not invalidated.

Example:

1. Board A is shared with Bob.
2. Bob’s board list cache includes Board A.
3. Owner removes Bob from sharing.
4. DB share link is soft-deleted.
5. Bob’s cache still contains Board A until expiry.

This is the clearest bug in the share service.

#### Problem D — owner-side share view may also be stale

The share service invalidates recipients but does not clearly invalidate the owner cache/view after share updates.

This may not affect gameplay board selection for the owner, because they still own the board, but any owner-side metadata/share display can become stale.

#### Why this matters

This affects shared custom boards and can cause:

- stale board names/details,
- deleted boards still appearing,
- unshared boards still appearing for removed users,
- inconsistent board-selection behaviour,
- confusing user experience around shared-board permissions.

Because board selection feeds game creation/setup, this is more than cosmetic.

#### Fix direction

Centralise board-skin cache invalidation around affected users.

For share updates, invalidate:

- owner,
- users added,
- users restored,
- users removed,
- current submitted user IDs.

Example structure:

```csharp
var affectedUserIds = toAdd.Select(x => x.UserId)
    .Concat(toRestore.Select(x => x.UserId))
    .Concat(toDelete.Select(x => x.UserId))
    .Concat(userIds)
    .Distinct()
    .ToList();

_boardCacheService.Invalidate(); // owner/current user

foreach (var userId in affectedUserIds)
{
    _boardCacheService.Invalidate(userId, bypassAdminCheck: true);
}
```

For board edits/deletes/space changes, fetch active shared recipients and invalidate them too:

```csharp
private async Task InvalidateBoardSkinCaches(string boardSkinId)
{
    _boardCacheService.Invalidate();

    var sharedUserIds = await _repos.GetRepository<SharedBoardSkin>()
        .AsQueryable()
        .FilterDeleted(DeletedQueryType.OnlyActive)
        .Where(s => s.BoardSkinId == boardSkinId)
        .Select(s => s.UserId)
        .Distinct()
        .ToListAsync();

    foreach (var userId in sharedUserIds)
    {
        _boardCacheService.Invalidate(userId, bypassAdminCheck: true);
    }
}
```

For deletion, capture share recipients before soft-deleting the share links, or query using an `All` deleted filter after deletion.

#### Suggested regression checks

- Shared user sees updated board after owner edits the board.
- Shared user no longer sees board after owner removes share.
- Shared user no longer sees board after owner deletes board.
- Owner still sees correct board state after share changes.
- Cache invalidation works for add, restore, remove, edit, delete.

---

### UM-007 — `/sitemap.xml` trusts and emits the request host unescaped

**Classification:** Major  
**Type:** SEO/security hardening / host-header trust  
**Status:** Fixed

#### Resolution

Fixed in `UltimateMonopoly/Program.cs`. The `/sitemap.xml` handler no longer derives the base URL
from `ctx.Request.Scheme`/`ctx.Request.Host`. It now reads the configured canonical origin
`Routes:WebUrl` (`= https://www.monappoly.com/`, the same value the pages use for their
`<link rel="canonical">`), trims the trailing slash, and XML-escapes each `<loc>` via
`System.Security.SecurityElement.Escape`. `HttpContext` is no longer taken; `IConfiguration` is
injected instead. Regardless of which host reaches the endpoint (production, `.co.uk` alias,
staging, localhost, tunnel), the sitemap emits only the canonical URLs:

```text
https://www.monappoly.com/
https://www.monappoly.com/Rules
https://www.monappoly.com/Guides
```

Falls back to the hardcoded canonical (matching the existing page pattern
`Config["Routes:WebUrl"] ?? "https://www.monappoly.com/"`) if the config key is absent.

#### Main file involved

- `UltimateMonopoly/Program.cs`

#### Summary

The sitemap endpoint builds absolute URLs from the incoming request:

```csharp
var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
```

It then interpolates that value directly into XML:

```csharp
$"""
<url>
    <loc>{baseUrl}{route}</loc>
    <priority>{priority}</priority>
</url>
"""
```

`Request.Host` comes from the HTTP `Host` header. Even if production routing makes this hard to abuse, sitemap generation should not be request-host dependent.

#### Why this matters

A sitemap is a canonical public document. It should emit the canonical public origin, not whatever host reached the endpoint.

Current behaviour can cause:

- wrong sitemap URLs on staging/local/alias hosts,
- canonical SEO confusion,
- malformed XML if an unexpected host value is accepted,
- host-header poisoning style risk,
- future fragility if deployment topology changes.

The user confirmed this should be major and that route/canonical config already exists.

#### Fix direction

Use configured canonical origin/base URL instead of request-derived host.

Example:

```csharp
var baseUrl = builder.Configuration["Site:CanonicalBaseUrl"]?.TrimEnd('/');
```

Then generate XML safely using an XML writer, or at minimum XML-escape the final loc value:

```csharp
SecurityElement.Escape($"{baseUrl}{route}")
```

A more robust approach is to use `XmlWriter` rather than interpolated XML strings.

#### Expected behaviour after fix

Regardless of whether the request hits:

- production canonical host,
- `.co.uk` alias,
- staging hostname,
- localhost,
- Cloudflare tunnel hostname,

the sitemap should emit only the configured canonical public URL, for example:

```text
https://monappoly.com/
https://monappoly.com/game-rules
...
```

#### Suggested regression checks

- Request `/sitemap.xml` using production host: emits canonical configured base URL.
- Request `/sitemap.xml` using alias/staging/local host: still emits canonical configured base URL.
- Sitemap XML validates.
- Routes are encoded safely.

---

### UM-009 — Live game hydration can select soft-deleted snapshots

**Classification:** Major (largely defensive — see resolution)  
**Type:** Soft-delete filtering / live cache correctness  
**Status:** Fixed

#### Resolution

Fixed in `GameCacheService.HydrateAsync`. The snapshot query now applies
`FilterDeleted(DeletedQueryType.OnlyActive)` and excludes snapshots whose turn is soft-deleted
(`!s.Turn.IsDeleted`), matching the active-only game query above it and every other snapshot access
in the codebase. Previously it selected the latest snapshot by turn number with no soft-delete
filter, so an active game whose latest snapshot had been soft-deleted would rebuild the live
`GameCacheModel` from deleted `StateJson`.

**Reachability:** the admin revert (`AdminGameStateService.TryRevertToTurn`) **hard**-deletes later
snapshots/turns, so it never produced this state. The reachable soft-delete producer is
`SnapshotCleanupJob` (the "AutoDelete = soft" retention pass, which `SoftDeleteRangeAsync`s
snapshots and turns for concluded games). So the gap was largely defensive, but the hydrate path
should not depend on cleanup never leaving a soft-deleted latest snapshot — now it doesn't.

#### Main file involved

- `UltimateMonopoly/Services/Cache/GameCacheService.cs`

#### Summary

The live game hydration path selects the latest snapshot by game ID and turn number, but does not filter out soft-deleted snapshots:

```csharp
var snapshot = await _repos.GetRepository<GameSnapshot>()
    .AsQueryable()
    .Where(s => s.GameId == gameId)
    .Include(s => s.Turn)
    .OrderByDescending(s => s.Turn.TurnNumber)
    .FirstOrDefaultAsync();
```

The game itself is filtered to active only, but the snapshot query is not.

Elsewhere, snapshot access commonly uses:

```csharp
.FilterDeleted(DeletedQueryType.OnlyActive)
```

The live cache hydrate path should be at least as strict.

#### Why this matters

If an active game row exists and the latest snapshot by turn number is soft-deleted, hydration can rebuild the live `GameCacheModel` from deleted `StateJson`.

Normal current cleanup paths may not create that state often, but live runtime hydration should not depend on that assumption.

Risk conditions include:

- future admin tooling,
- manual data repair,
- failed/partial maintenance operation,
- future snapshot-retention logic,
- inconsistent soft-delete cleanup,
- game rollback/debug tools.

Because hydration reconstructs live gameplay state, using a deleted snapshot is a major correctness risk.

#### Fix direction

Filter snapshots to active only in `GameCacheService.HydrateAsync()`:

```csharp
var snapshot = await _repos.GetRepository<GameSnapshot>()
    .AsQueryable()
    .FilterDeleted(DeletedQueryType.OnlyActive)
    .Where(s => s.GameId == gameId)
    .Include(s => s.Turn)
    .OrderByDescending(s => s.Turn.TurnNumber)
    .FirstOrDefaultAsync();
```

Consider also filtering the included turn:

```csharp
var snapshot = await _repos.GetRepository<GameSnapshot>()
    .AsQueryable()
    .FilterDeleted(DeletedQueryType.OnlyActive)
    .Where(s => s.GameId == gameId && !s.Turn.IsDeleted)
    .Include(s => s.Turn)
    .OrderByDescending(s => s.Turn.TurnNumber)
    .FirstOrDefaultAsync();
```

#### Suggested regression checks

- Hydration ignores soft-deleted snapshots.
- Hydration chooses latest active snapshot, not latest deleted snapshot.
- Hydration handles no active snapshots safely.
- Hydration handles deleted/invalid associated turn defensively.

---

### UM-014 — Admin draw completion records the game update as system, not admin

**Classification:** Major  
**Type:** Admin audit integrity bug  
**Status:** Fixed

#### Resolution

Fixed by threading the `isAdmin` flag from the caller rather than hardcoding it. `TryDrawGameByAdmin`
gained a `bool isAdmin = false` parameter (interface + impl) that it forwards to `ConcludeGame`.
`GameManagementService.DrawGame` (the real admin action, behind `AuthCheck()`) now passes
`isAdmin: true`, so the `Game`/`GamePlayer` audit rows attribute to the acting admin — confirmed via
the JC.Core repo contract: a `null` audit `userId` "Falls back to `IUserInfo.UserId`" (the current
admin), while `IUserInfo.SYSTEM_USER_ID` is passed explicitly for the system path.

**Important:** a blanket `isAdmin: true` inside `TryDrawGameByAdmin` would have been wrong — the
method is *also* called by `GameAbandonmentJob` (a background Hangfire sweep with no authenticated
user), which is deliberately attributed to System. That caller keeps the default `isAdmin: false`,
so `null → IUserInfo.UserId` doesn't degrade to `MissingUserInfoId` (`<NONE>`) there. The
`AdminActionLog` draw entry was already written and is unchanged; both audit streams now agree.

#### Main file involved

- `UltimateMonopoly/Services/GameEngine/GameCompletionService.cs`

#### Summary

The admin draw path calls `ConcludeGame()` without passing the admin flag:

```csharp
public async Task<bool> TryDrawGameByAdmin(GameEngine engine)
{
    ...
    await ConcludeGame(gameCache, game);
    return true;
}
```

But `ConcludeGame()` has an `isAdmin` parameter:

```csharp
private async Task ConcludeGame(GameCacheModel gameCache, Game game, bool isAdmin = false)
```

Inside `ConcludeGame()`, the audit user is selected like this:

```csharp
var userId = isAdmin ? null : IUserInfo.SYSTEM_USER_ID;

await _repos.GetRepository<Game>()
    .UpdateAsync(game, userId, saveNow: false);

await _repos.GetRepository<GamePlayer>()
    .UpdateRangeAsync(game.Players, userId, saveNow: false);
```

Because the admin caller does not pass `isAdmin: true`, the game and player entity audit trail is written as system, not the acting admin.

#### Important nuance

An admin action log is still written:

```csharp
await _adminLogService.LogGameDrawn(gameId);
```

So the action is not completely unaudited. The issue is that two audit sources disagree:

```text
AdminActionLog:
    Admin declared game draw.

Game/GamePlayer audit fields:
    System updated game/player rows.
```

For an admin area focused on traceability, that inconsistency is significant.

#### Why this matters

Admin-forced game completion is a privileged action. Entity audit should correctly reflect that the admin caused the update.

Bad audit attribution can make later investigation harder:

- Was the game concluded by natural gameplay?
- Was it system automation?
- Was it admin intervention?
- Which admin caused the state change?

The current code partly answers this via `AdminActionLog`, but the entity audit trail itself is misleading.

#### Fix direction

Change the admin path from:

```csharp
await ConcludeGame(gameCache, game);
```

to:

```csharp
await ConcludeGame(gameCache, game, isAdmin: true);
```

Consider renaming the parameter to make its purpose harder to miss:

```csharp
private async Task ConcludeGame(
    GameCacheModel gameCache,
    Game game,
    bool useCurrentUserAudit = false)
```

or split system/gameplay completion from admin-forced completion into separate explicit methods.

#### Suggested regression checks

- Natural game completion updates game/player rows as system.
- Admin-forced draw updates game/player rows as the current admin.
- Admin action log still records the draw.
- Both audit streams agree on admin intervention.

---

### UM-017 — Remove dead “Invite to Game” buttons from Friends UI

**Classification:** Major  
**Type:** Release polish / dead UI affordance  
**Status:** Fixed

#### Resolution

Removed both dead invite buttons from `Areas/Social/Pages/Friends/Index.cshtml`: the card-view
"Invite to Game" (the `Message` link now takes the `flex-grow-1` it vacated so the row stays
balanced) and the list-view "Invite" (the `btn-group` keeps `Message` + the actions dropdown).
Confirmed no JS referenced them (`friends.js` has no invite wiring) and the game-invite feature/
backend remains deferred — not implemented here. Builds clean.

**Out of scope (left as-is):** the header copy "…invite players, chat, and grow your circle."
(`Index.cshtml:13`) still says "invite players" — ambiguous enough to read as "invite people to the
app", and the issue scoped this to the buttons only. Flag for a copy pass if game-invites stay
deferred.

#### Main file involved

- `UltimateMonopoly/Areas/Social/Pages/Friends/Index.cshtml`

#### Summary

The Friends page contains visible invite-to-game buttons, but the game invite feature itself is deferred/not implemented.

Examples:

```html
<button class="btn btn-primary btn-sm flex-grow-1" type="button">Invite to Game</button>
```

and:

```html
<button class="btn btn-primary" type="button">Invite</button>
```

The feature/backend is deferred, but the button remaining in V1 UI is a problem.

#### Important scope

Do **not** implement game invites as part of this issue.

The agreed issue is only:

```text
Remove the dead Invite to Game button(s) from the Friends UI.
```

The game-invite feature itself is deferred and should not be included in this bug-fix push.

#### Why this matters

A visible button that appears actionable but does nothing creates a broken-feeling product experience.

Users may assume:

- the app is broken,
- their click failed,
- their friend did not receive an invite,
- the lobby/game invite system exists but malfunctioned.

That is worse than simply not showing the feature yet.

#### Fix direction

Remove or hide the invite-to-game buttons for V1.

Possible approaches:

1. Remove the markup entirely.
2. Hide behind a feature flag that defaults off.
3. Replace with a disabled “Coming soon” state only if you intentionally want users to know it is planned.

The cleanest V1 release fix is to remove the buttons.

#### Suggested regression checks

- Friends page no longer shows clickable game-invite controls.
- No dead button remains in desktop layout.
- No dead button remains in mobile/card layout.
- No JavaScript references expect the removed elements.
- Layout still looks balanced after removal.

---

### UM-018 — Remove stale admin sidebar/comment scaffolding

**Classification:** Major  
**Type:** Release polish / stale WIP scaffolding  
**Status:** Genuine

#### Main files involved

- `UltimateMonopoly/Areas/Admin/Pages/Shared/_AdminSidebar.cshtml`
- `UltimateMonopoly/Areas/Admin/Pages/Index.cshtml`
- `UltimateMonopoly/Areas/Admin/Models/ViewModels/Dashboard/HubAndTrendWidgets.cs`

#### Summary

The admin sidebar contains stale comments/scaffolding suggesting links are placeholders:

```cshtml
Item links are placeholders (#) until each feature phase lands
```

But the sidebar links are now real:

```cshtml
asp-page="/Users/Dashboard"
asp-page="/Reports/Index"
asp-page="/Games/Dashboard"
asp-page="/Audit/Dashboard"
asp-page="/Logs/Dashboard"
```

There is also dashboard fallback scaffolding around “coming soon” widgets. Some of that may still be useful defensively, but stale comments that imply unfinished placeholder links should be removed.

#### Why this matters

This is not a runtime gameplay bug, but it is a release-quality issue.

The admin area is a trust/accountability surface. Stale WIP comments and placeholder wording make the codebase look less complete and can mislead future maintainers.

The agreed reason this is major is that it is quick and easy to remove in the next bug-fix push.

#### Fix direction

At minimum:

- remove the stale sidebar comment,
- remove or update any comment that says admin links are placeholders when they are now real,
- review visible “coming soon” admin scaffolding and remove anything no longer applicable.

Keep defensive fallback code only if it still serves a real purpose.

#### Suggested regression checks

- Admin sidebar contains no stale placeholder comments.
- Admin dashboard contains no visible “coming soon” tile for an implemented spoke.
- Existing admin links still work.
- No layout regression after removing stale scaffolding.

---

## Minor Issues

### UM-002 — Modified dice rolls are emitted/stored as the original roll

**Classification:** Minor  
**Type:** Event/projection accuracy issue  
**Status:** Genuine

#### Summary

When a dice roll is modified, the emitted/stored roll information can still reflect the original roll rather than the effective modified roll.

This is not currently considered major because it does not necessarily break gameplay execution, but it is still worth noting because downstream consumers may interpret the stored/emitted roll as the actual roll used.

#### Why this matters

The issue can affect:

- game event logs,
- stats projections,
- debugging,
- audit/history views,
- any later replay-style tooling,
- user-facing explanation of why movement happened.

If the game state uses one value but the event says another, debugging becomes unnecessarily confusing.

#### Fix direction

Make the distinction explicit:

- original roll,
- modified/effective roll,
- reason/source of modification.

Avoid overwriting meaning ambiguously. Either store both values or ensure events clearly emit the effective gameplay value.

#### Suggested regression checks

- Unmodified roll emits/stores one clear value.
- Modified roll records original and effective values, or records only effective value with clear naming.
- Movement/events/stats agree on the effective roll used.

---

### UM-004 — Full game command queue silently drops commands while callers still see success

**Classification:** Minor  
**Type:** Real-time UX / command reliability issue  
**Status:** Genuine

#### Main file involved

- `UltimateMonopoly/Services/GameEngine/GameExecutor.cs`

#### Summary

The per-game command queue is bounded. When it is full, the executor logs and drops the command:

```csharp
case EnqueueOutcome.Full:
    _logger.LogWarning("Game {GameId} work queue is full ({Capacity}); dropping command.",
        gameId, GamePump.Capacity);
    return;
```

But the public enqueue API is void:

```csharp
void Enqueue(string gameId, GameWorkItem work);
```

So callers cannot know the command was not accepted.

SignalR hub methods then return success after enqueueing, for example:

```csharp
_gameService.EnqueueTurn(gameId, Context.UserIdentifier);
return true;
```

The browser/client can receive `true` even though the command was dropped.

#### Exact flow

Example: `StartTurn`.

1. Client calls SignalR `StartTurn`.
2. Hub loads engine/cache.
3. Hub does optimistic gate check.
4. Hub calls `_gameService.EnqueueTurn(...)`.
5. `GameService.EnqueueTurn()` calls `_executor.Enqueue(...)`.
6. `GameExecutor.Enqueue()` gets the per-game pump.
7. The pump queue is bounded to capacity 10.
8. Enqueue uses `TryWrite`.
9. If queue is full, `TryWrite` returns false.
10. Executor logs and drops.
11. Hub still returns `true`.

#### Why this matters

This is mainly a client truthfulness/UX issue.

A player can click a valid action, receive apparent success, and then nothing happens because the command was never queued.

The server-side state is mostly protected because queued commands re-check validity when they execute. The problem is the action disappearing silently.

#### Likely triggers

- double-click/spam-click before cache updates,
- prompt waiting while pump is occupied,
- repeated host/admin actions,
- multiple tabs,
- reconnect/stale UI situations.

#### Affected surfaces

Likely affected command surfaces include:

- start turn,
- end turn,
- leave jail/pay/card,
- play card,
- propose deal,
- declare bankruptcy,
- draw game,
- declare winner,
- portfolio commands such as mortgage, unmortgage, unreserve, build, build set/all, sell, sell set/all, repay loan.

#### Fix direction

Make enqueue return an outcome.

Example:

```csharp
public enum GameCommandQueueResult
{
    Accepted,
    Busy,
    Closed
}
```

Then hub methods should only return success when the command was accepted.

Minimum viable fix:

```csharp
bool Enqueue(...)
```

Better fix:

```csharp
GameCommandQueueResult Enqueue(...)
```

Then the UI can show a “game is busy, retry shortly” type result.

#### Suggested regression checks

- Queue accepted command returns success.
- Full queue returns failure/busy to caller.
- Hub does not return `true` when command is dropped.
- UI can handle busy response.
- Existing game-command validation still occurs inside the pump.

---

### UM-006 — Duplicate board-skin share recipients can create duplicate share rows

**Classification:** Minor  
**Type:** Defensive input normalization / service reliability issue  
**Status:** Genuine

#### Main file involved

- `UltimateMonopoly/Services/BoardSkins/BoardSkinShareService.cs`

#### Summary

`TryShareBoardSkin()` validates a distinct copy of submitted user IDs, but later uses the original non-distinct list to create share rows.

Validation:

```csharp
private async Task<bool> ValidUserIds(List<string> userIds)
{
    var distinct = userIds.Distinct().ToList();
    ...
}
```

Mutation:

```csharp
var toAdd = userIds.Where(u => !existingUserIds.Contains(u))
    .Select(u => new SharedBoardSkin(skinId, u))
    .ToList();
```

If a crafted/manual request posts the same friend ID twice and that friend is not already linked, `toAdd` can contain duplicate `SharedBoardSkin` entities.

#### Why this matters

The DB composite primary key protects actual duplicate persistence:

```csharp
[PrimaryKey(nameof(BoardSkinId), nameof(UserId))]
public class SharedBoardSkin : AuditModel
```

So this is not a corruption issue. The likely result is a failed save and an error message.

Still, the service validates one list but mutates with another, which is a robustness issue.

#### Fix direction

Normalize `userIds` once at the start of `TryShareBoardSkin()` and use only that normalized list:

```csharp
userIds = userIds
    .Where(id => !string.IsNullOrWhiteSpace(id))
    .Distinct()
    .ToList();
```

Optionally trim IDs if appropriate:

```csharp
userIds = userIds
    .Select(id => id.Trim())
    .Where(id => id.Length > 0)
    .Distinct(StringComparer.Ordinal)
    .ToList();
```

#### Suggested regression checks

- Duplicate submitted friend IDs do not produce duplicate share entities.
- Duplicate submitted friend IDs do not fail the whole share operation.
- Add/remove/restore share behaviour still works.
- DB primary key remains as final protection.

---

### UM-010 — Per-game hydration locks survive normal cache expiry

**Classification:** Minor  
**Type:** Memory lifecycle / cache sidecar cleanup issue  
**Status:** Genuine

#### Main file involved

- `UltimateMonopoly/Services/Cache/GameCacheService.cs`

#### Summary

Game cache entries expire after a 2-hour sliding window:

```csharp
private static readonly TimeSpan GameCacheSliding = TimeSpan.FromHours(2);

private static MemoryCacheEntryOptions GameEntryOptions() =>
    new() { SlidingExpiration = GameCacheSliding };
```

But hydration locks are stored in a static dictionary:

```csharp
private static readonly ConcurrentDictionary<string, SemaphoreSlim> HydrationLocks = new();
```

Locks are created per game:

```csharp
var gate = HydrationLocks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
await gate.WaitAsync();
```

Explicit invalidation removes the lock:

```csharp
public void Invalidate(string gameId)
{
    _cache.Remove(GetKey(gameId));
    HydrationLocks.TryRemove(gameId, out _);
}
```

But normal cache expiry does not call `Invalidate(gameId)`, so the lock entry can remain for the lifetime of the process.

#### Why this matters

This is not a gameplay bug and will not corrupt game state.

It is a small memory/lifecycle mismatch:

```text
GameCacheModel lifetime: sliding 2 hours
HydrationLocks lifetime: process lifetime unless explicit invalidation
```

One small semaphore per hydrated game is not catastrophic, but it is unnecessary accumulation over a long-running process.

#### Fix direction

Register a post-eviction callback on the game cache entry to remove the corresponding hydration lock.

Example:

```csharp
private MemoryCacheEntryOptions GameEntryOptions(string gameId) =>
    new MemoryCacheEntryOptions
    {
        SlidingExpiration = GameCacheSliding
    }.RegisterPostEvictionCallback((_, _, _, _) =>
    {
        HydrationLocks.TryRemove(gameId, out _);
    });
```

Use that when setting game cache entries:

```csharp
_cache.Set(GetKey(gameId), cache, GameEntryOptions(gameId));
```

Do **not** dispose the semaphore in the callback, because an in-flight hydrate may still hold it.

#### Suggested regression checks

- Lock is removed when cache entry expires.
- Lock is removed when cache entry is explicitly invalidated.
- Concurrent hydration still works.
- No `ObjectDisposedException` is introduced.

---

### UM-011 — Bulk build/sell “all” checks return true for empty/no-op sets

**Classification:** Minor  
**Type:** Command gating / UX correctness issue  
**Status:** Genuine

#### Main files involved

- `MP.GameEngine/Models/Snapshot/GameModel.cs`
- `MP.GameEngine/Services/SubSystems/BuildingService.cs`
- `UltimateMonopoly/Areas/Game/Pages/Shared/Play/_PlayerProfileView.cshtml`

#### Summary

These methods use `.All(...)` over the owned sets:

```csharp
public bool CanIncreaseRentLevelForAll(string playerId)
{
    var owned = GetOwnedProperties(playerId, includeMortgaged: false, includeReserved: false);
    return PropertySetHelper.GetOwnedSets(playerId, owned)
        .All(set => CanIncreaseRentLevelForAllInSet(playerId, set));
}
```

```csharp
public bool CanDecreaseRentLevelForAll(string playerId)
{
    var owned = GetOwnedProperties(playerId, includeMortgaged: false, includeReserved: false);
    return PropertySetHelper.GetOwnedSets(playerId, owned)
        .All(set => CanDecreaseRentLevelForAllInSet(playerId, set));
}
```

LINQ `.All(...)` returns true for an empty collection.

So a player with zero complete eligible sets can be reported as able to build/sell all.

#### Exact bad flow

`_PlayerProfileView.cshtml` uses:

```csharp
var canBuildAll = canPortfolio && game.CanIncreaseRentLevelForAll(playerId);
var canSellAll = canPortfolio && game.CanDecreaseRentLevelForAll(playerId);
```

This can enable global buttons such as:

```html
data-cmd="buildall"
data-cmd="sellall"
```

The server-side command can also pass the same check, then produce an empty/no-op flow and potentially a prompt like spending/receiving £0.

#### Why this matters

This does not appear to grant free buildings or money, because the execution index list is empty. The problem is that the app advertises an action as available when there is nothing eligible to act on.

That creates:

- bogus enabled buttons,
- no-op commands,
- confusing £0 prompts,
- unnecessary queue/work items.

#### Fix direction

Require at least one eligible complete set before returning true.

Example:

```csharp
public bool CanIncreaseRentLevelForAll(string playerId)
{
    var owned = GetOwnedProperties(playerId, includeMortgaged: false, includeReserved: false);
    var sets = PropertySetHelper.GetOwnedSets(playerId, owned);

    return sets.Count > 0
           && sets.All(set => CanIncreaseRentLevelForAllInSet(playerId, set));
}
```

Same pattern for sell:

```csharp
public bool CanDecreaseRentLevelForAll(string playerId)
{
    var owned = GetOwnedProperties(playerId, includeMortgaged: false, includeReserved: false);
    var sets = PropertySetHelper.GetOwnedSets(playerId, owned);

    return sets.Count > 0
           && sets.All(set => CanDecreaseRentLevelForAllInSet(playerId, set));
}
```

Add defensive guards in command execution:

```csharp
if (ownedSetsProperties.Count == 0)
    return;
```

and/or:

```csharp
if (indexes.Count == 0)
    return;
```

#### Suggested regression checks

- Player with no properties cannot build all/sell all.
- Player with incomplete sets cannot build all/sell all.
- Player with valid complete eligible sets can build all/sell all.
- No £0 prompt appears for no-op bulk actions.

---

### UM-012 — Set/all sell paths underpay double hotels if a double hotel reaches bulk execution

**Classification:** Minor  
**Type:** Bulk sell value calculation fragility  
**Status:** Genuine, mostly blocked by current rules

#### Main file involved

- `MP.GameEngine/Services/SubSystems/BuildingService.cs`

#### Summary

Single-property sell handles double hotels correctly.

It detects double hotel before downgrading:

```csharp
if (property.RentLevel == RentLevel.DOUBLE_HOTEL)
{
    doubleHotelValue = PropertySetHelper.GetDoubleHotelSellValue(boardIndex, engine.Cache.Board, streetEffect);
    value = (uint)doubleHotelValue;
}
```

Then it passes the special value into the private sell method.

Bulk set/all sell paths do not do this. They calculate normal set sell values and later call the private sell method without a double-hotel override.

Inside private sell execution, the property is downgraded before value is calculated:

```csharp
property.RentLevel -= 1;

var value = PropertySetHelper.GetSellValue(property.BoardIndex, engine.Cache.Board, streetEffect);
```

If a `DOUBLE_HOTEL` property reaches this path, it can be paid as a normal one-step sell rather than using the double-hotel sell value.

#### Important current-rule nuance

Current validation appears to block normal bulk-set double-hotel selling.

Because only one property in a set can be double hotel, the others are lower. Bulk sell checks all properties in the set, and lower properties cannot decrease while another property has a higher rent level.

So normal UI/rule flow should only allow selling the double-hotel property individually, which is correctly handled.

This makes the issue minor/defensive rather than a current exploitable economy bug.

#### Why this still matters

The private execution method is fragile. It depends on upstream validation never allowing a double hotel into a bulk sell path.

Future rule changes, admin/debug tools, weird state, or direct service calls could bypass that assumption.

#### Fix direction

Make private per-property sell execution calculate the correct value based on pre-decrement state.

Example:

```csharp
var wasDoubleHotel = property.RentLevel == RentLevel.DOUBLE_HOTEL;

property.RentLevel -= 1;

var space = engine.Cache.Board.GetBoardSpace(property.BoardIndex);
var streetEffect = engine.Cache.Game.HasStreetEffect(player.PlayerId, (PropertySet)space.PropertySet);

var value = wasDoubleHotel
    ? PropertySetHelper.GetDoubleHotelSellValue(property.BoardIndex, engine.Cache.Board, streetEffect)
    : PropertySetHelper.GetSellValue(property.BoardIndex, engine.Cache.Board, streetEffect);

await _transactionService.ReceiveForSell(engine, player, value, property.BoardIndex, ct);
```

This would allow removal of the `doubleHotelValue` parameter entirely.

#### Suggested regression checks

- Individual double-hotel sell pays correct double-hotel value.
- Bulk sell remains blocked under current normal double-hotel set state.
- If a double-hotel property is ever sold through shared private execution, it still pays correct value.
- Normal house/hotel sell values are unchanged.

---

### UM-013 — Deal offers accept duplicate property indexes

**Classification:** Minor  
**Type:** Server-side validation / crafted-input robustness issue  
**Status:** Genuine

#### Main file involved

- `MP.GameEngine/Services/SubSystems/DealService.cs`

#### Summary

The direct deal command validates that every submitted property is dealable, but does not validate that the submitted list is distinct.

The relevant method:

```csharp
private static bool ValidOffer(GameModel game, PlayerModel player, uint money, IReadOnlyList<ushort> properties)
{
    if (money > player.Money) return false;

    var dealable = game.TradableProperties(player.PlayerId, includeMortgaged: true)
        .Select(p => p.BoardIndex).ToHashSet();

    return properties.All(dealable.Contains);
}
```

This accepts:

```json
{
  "propertiesFromProposer": [12, 12]
}
```

as long as property `12` is dealable by the proposer.

#### Important nuance

The shortfall deal prompt path already protects this:

```csharp
if (selected.Distinct().Count() != selected.Count) return false;
```

So the issue is specifically the direct turn-boundary `ProposeDeal` SignalR path.

#### What happens if duplicates get through

`RunDeal()` processes each submitted index:

```csharp
foreach (var i in contents.PropertiesFromProposer)
{
    var property = engine.Cache.Game.GetPropertySpace(i);
    if(property == null)
        continue;

    _propertyTransferService.Transfer(engine, player, counterpartyPlayer, property);
}
```

The first transfer moves the property.

The second transfer processes the same property again. State is likely protected because there is still only one `PropertyModel`, and ownership methods may return early. However, transfer receipts/events can still be emitted twice, which can pollute stats/projections.

#### Why this matters

Normal UI likely uses checkboxes/select controls and will not naturally submit duplicates.

But SignalR calls are client-controllable. Server-side validation should not rely on the UI to provide a clean list.

#### Fix direction

Make `ValidOffer()` reject duplicate property indexes:

```csharp
private static bool ValidOffer(GameModel game, PlayerModel player, uint money, IReadOnlyList<ushort> properties)
{
    if (money > player.Money) return false;

    if (properties.Distinct().Count() != properties.Count)
        return false;

    var dealable = game.TradableProperties(player.PlayerId, includeMortgaged: true)
        .Select(p => p.BoardIndex)
        .ToHashSet();

    return properties.All(dealable.Contains);
}
```

Also consider hardening property-transfer receipt emission so receipts are only emitted when ownership actually changes.

#### Suggested regression checks

- Duplicate proposer property indexes are rejected.
- Duplicate counterparty property indexes are rejected.
- Valid distinct property lists still pass.
- Rejected deal does not emit transfer receipts.
- Shortfall prompt validator and direct deal validator stay aligned.
