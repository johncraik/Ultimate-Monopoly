# C1 — Admin & Moderation Area

The operational backbone for running the app in public: the admin UI + services that let
SystemAdmins/Admins **see what's happening and act on it**. This is the single largest V1 piece
(roadmap C1), so it gets its own doc and a **phased build** — not one shot.

**Status:** design. Phased (see §13). Some areas are deliberately **deferred** but planned here:
the **Audit Trail** (§9 — blocked on a JC.Core `AuditEntry.EntityKey` column John is adding) and the
**log viewers** (§10 — admin/comms logs, several gated on A1/E1). Everything else is build-ready.

---

## 1. Purpose & Scope

Once real users are on the app it must be **operable**. Admins need to view and act on:

1. **Users** — roles, account state, removal.
2. **Reports** — block-reports, with quick moderation actions.
3. **Games** — a global view + lifecycle actions, and a retention policy.
4. **Rules & Turn Tax** — edit the player-facing rule text and the turn-tax config.
5. **Audit trail** — who changed what, by table and by user *(deferred — §9)*.
6. **Logs** — admin-action, comms (email/notifications/messaging) *(deferred — §10)*.

**Out of scope:** analytics/usage dashboards (a later enhancement); anything a normal user already
does on their own profile.

---

## 2. The Decisions (locked)

So they're not re-litigated:

1. **Two-tier auth** — `Admin` (moderation + read) vs `SystemAdmin` (config + destructive). Matrix §4.
2. **One `AdminActionLog`** (`LogModel`) records **every** admin action — user, report, game, config.
   Not per-feature logs (§5).
3. **`AuditEntry` gains an `EntityKey`** column (JC.Core, John) — a **comma-separated list** of the
   record's primary-key value(s) (so composite keys store fine). Unblocks the per-record data trail.
   The audit-trail *feature* is **deferred** until it lands (§9).
4. **User delete is a hard `UserManager.DeleteAsync`**, leaving records **orphaned-by-id** (the
   social/game tables already use no-FK string user ids "to allow account deletion"). A separate
   recurring **`OrphanCleanupJob`** scrubs orphaned records — decoupled from the immediate delete (§6.3).
5. **Role changes propagate via a singleton refresh registry + middleware** (§6.2) — not a forced
   re-login.
6. **Admin settings live in `config/rules/settings.json`** (alongside `turnTax.json` / `rules.json`),
   read + saved through a `SettingsService` (§7.1).
7. **`ReportedUser` gains a `[Flags]` resolution enum** (`Open / AccountRestricted / AccountDisabled /
   AccountDeleted`) for filtering the queue (§5.2 / §6 reports).
8. **All logs (audit, admin, comms) are cleared by background jobs** (retention). Read-only viewers.

---

## 3. Area Shell

- A new **`Areas/Admin`** Razor Pages area (greenfield — none exists). Its own `_Layout` with a
  **sidebar nav** grouping the sections (Users · Reports · Games · Rules · Turn Tax · Audit · Logs).
- A nav entry to `/Admin` shown only to `Admin` / `SystemAdmin`.
- The global `FallbackPolicy` already requires auth; the area adds an **authorization policy**
  (`AdminArea` = in `Admin` *or* `SystemAdmin`) applied folder-wide
  (`Conventions.AuthorizeAreaFolder("Admin", "/", "AdminArea")`). **SystemAdmin-only** pages/actions
  carry an extra check (a `SystemAdminOnly` policy, or an inline `IUserInfo.IsInRole` guard).
- Every mutating admin handler writes an `AdminActionLog` entry (§5) as its last step.

---

## 4. Authorisation Matrix

| Capability | Admin | SystemAdmin |
|---|---|---|
| View audit trail + all logs | ✅ | ✅ |
| Reports — view + full management (act on them) | ✅ | ✅ |
| Apply / lift **Restricted** on a user | ✅ | ✅ |
| **Disable / enable** a user (`IsEnabled`) | ✅ | ✅ |
| Full **role management** (grant any role, incl. **Admin**) | ❌ | ✅ |
| **Delete** a user account | ❌ | ✅ |
| **Game management** (draw / cancel / delete) | ❌ | ✅ |
| **Rules + Turn Tax** editing | ❌ | ✅ |
| **Retention / settings** | ❌ | ✅ |

> Admin is **read + moderation**: it can see everything and resolve reports (restrict/disable), but
> can't delete users, touch config, or manage games. The non-destructive inverses of its allowed
> actions (lift Restricted, re-enable) are included. Granting **Admin** is SystemAdmin-only — admins
> can't escalate peers.

---

## 5. `AdminActionLog` — the cross-cutting accountability log

`AppUser` / `AppRole` are **not** `AuditModel`, and rules/turntax/settings are **files**, so none of
the admin actions land in the JC.Core audit trail. `AdminActionLog` fills that hole: an **immutable**
record of every admin action, written by every mutating admin handler.

```csharp
public class AdminActionLog : LogModel          // immutable; CreatedById = the acting admin, CreatedUtc = when
{
    [Key] public string Id { get; private set; } = Guid.NewGuid().ToString();
    public AdminActionType Action { get; set; }   // what was done
    public AdminTargetType TargetType { get; set; } // User / Game / Report / Config / Role
    public string? TargetId { get; set; }         // the user / game / report id, or config name
    public string? Detail { get; set; }           // human-readable summary or small JSON of the change
}

public enum AdminTargetType { User, Game, Report, Role, Config }

public enum AdminActionType
{
    RoleAdded, RoleRemoved, UserDisabled, UserEnabled, UserDeleted,   // users
    ReportResolved,                                                   // reports (detail = the resolution flags)
    GameDrawn, GameCancelled, GameDeleted,                            // games
    RulesUpdated, TurnTaxUpdated, SettingsUpdated                     // config
}
```

- `LogModel` → JC.Core skips create-audit (the log *is* the record), forbids update/soft-delete/
  restore, and `CreatedById`/`CreatedUtc` auto-populate from `IUserInfo` (the admin).
- **Built from Phase 1** (every handler writes one). The **viewer** is part of the deferred logs work
  (§10), but the data accrues from day one.
- Cleared by a retention background job alongside the other logs (§2.8).

---

## 6. User Management

*Mostly JC.Identity; the propagation + cleanup mechanisms are the new design.*

### 6.1 List & act
- **List** — `UserManager<AppUser>.Users` queryable → `ToPagedListAsync`; show username, display name,
  email, `IsEnabled`, roles, W/L/D, last login. Search by username/email.
- **Roles** — the assignable set from `SystemRoles.GetAllRoles<AppRoles>()`
  (SystemAdmin / Admin / Restricted / HiddenUser, each with a description). Add/remove via
  `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync`.
  - **Admin** may only toggle **Restricted**; **SystemAdmin** may grant/remove **any** role
    (including Admin) — enforced server-side, not just in the UI.
- **Disable / enable** — `user.IsEnabled = false/true` + `UserManager.UpdateAsync`. JC.Identity's
  middleware enforces `IsEnabled` on the user's next request.
- **Delete** — SystemAdmin only; see §6.3.

### 6.2 Propagating a role change to an active session
Role gates read **claims** (the auth cookie), so applying `Restricted` to a logged-in user wouldn't
bite until their cookie refreshes. Mechanism:

- A **singleton `IPendingClaimsRefresh`** registry (`ConcurrentDictionary<string, byte>` of user ids).
  When an admin changes a user's roles (or `IsEnabled`), that user's id is **flagged**.
- A **lightweight middleware** (after JC.Identity's `UserInfoMiddleware`) checks whether the *current*
  request's user id is flagged; if so it calls **`SignInManager.RefreshSignInAsync(currentUser)`** —
  for **their own** session, in **their own** request — and clears the flag.
- This is the safe shape: the refresh always re-issues the **current** request's cookie (never a
  target user's — that was the session-swap footgun from the E2 work). `IsEnabled` is already enforced
  by middleware, so the registry primarily serves **role** changes.

### 6.3 Delete + orphan cleanup
- **Delete is immediate and simple**: `UserManager.DeleteAsync(user)` (hard) + an `AdminActionLog`
  entry. The user's games/stats/friends/blocks/board-skins remain, attributed to a now-missing id
  (the tables use plain string user ids, no nav FK — by design).
- **`OrphanCleanupJob`** (recurring Hangfire) does the heavy scrub **separately** from the delete
  action: it scans the user-id-referencing tables (games + `GamePlayer`, `PlayerGameStat`, `Friend`,
  `FriendRequest`, `BlockedUser`, `ReportedUser`, `BoardSkin`, `SharedBoardSkin`, …) for ids not in
  `AspNetUsers`, and soft-deletes / removes them in chunked transactions. Splitting it keeps the
  delete action snappy and the large query/transaction off the request path.

---

## 7. Report Management

`ReportedUser` (`AuditModel`: `BlockedId` FK→`BlockedUser`, `Reason`, `Message`) exists; there's no
"list all reports" query yet.

### 7.1 Resolution state
Add a `[Flags]` enum + property to `ReportedUser` (migration) so the queue is filterable:

```csharp
[Flags]
public enum ReportResolution { Open = 0, AccountRestricted = 1, AccountDisabled = 2, AccountDeleted = 4 }
```

- A report starts **Open**. Acting on it accumulates flags (you can both restrict **and** disable →
  `AccountRestricted | AccountDisabled`). `AccountDeleted` is effectively terminal.
- Default queue filter = **Open**; "handled" = any non-Open.
- When an admin acts on the **reported user**, the resolution is applied to **all that user's open
  reports** (the action is user-global), and an `AdminActionLog` (`ReportResolved`, `Detail` = the
  flags) is written.

### 7.2 The queue
- New paginated query over `ReportedUser` (active), joined to **reporter** (`BlockedUser.CreatedById`)
  and **reported** (`BlockedUser.BlockedUserId`), surfacing reason, message, date, resolution. Filter
  by resolution + reported user.
- **Quick actions** (both tiers): apply **Restricted**, **disable** the reported user — reuse §6.
  **SystemAdmin** additionally gets **delete** (§6.3). Every action → resolution flag + `AdminActionLog`.

---

## 8. Game Management *(SystemAdmin only)*

### 8.1 Retention settings (`config/rules/settings.json`)
- A `settings.json` file beside `turnTax.json` / `rules.json`, read + written via a new
  **`SettingsService`** (same shape as `TurnTaxService`: `Import()` at startup into a singleton, a
  `Save()` that writes the file and refreshes the in-memory copy).
- First setting: **`GameRetentionMonths`** — `0` = keep games **indefinitely**; `> 0` = clear games
  older than *N* months.
- A recurring **`GameRetentionJob`** (Hangfire) reads it and, when enabled, soft-deletes concluded
  games (Finished / Cancelled) older than the threshold via `GameService.TryDeleteGame` (which already
  cascades players/turns/events/snapshots). `0` → the job no-ops.

### 8.2 Admin game list + actions
- New paginated **admin query**: *all* games (not per-user) — id, name, **creator** (id + display),
  state, **players (+ user ids)**, board, turn count, timestamps.
- **State-gated actions** (each → `AdminActionLog` + a confirm):

  | State | Actions |
  |---|---|
  | Setup | **Cancel**, **Cancel + Delete** |
  | InPlay | **Draw**, **Cancel**, **Cancel + Delete** |
  | Finished | **Delete** |
  | Cancelled | **Delete** |

  - **Draw** (InPlay only) — conclude as `Drawn` via `GameCompletionService` (expose an admin
    declare-draw path).
  - **Cancel** (Setup/InPlay) — `GameService.TryCancelGame` (→ Cancelled, tears down runtime).
  - **Cancel + Delete** (Setup/InPlay) — cancel then `TryDeleteGame`.
  - **Delete** (Finished/Cancelled) — `TryDeleteGame` (soft-delete + cascade). *Extend its allowed
    states from "Cancelled only" to {Finished, Cancelled}.*
  - `ForceRefresh` already exists if a wedged live game needs a client reload.

---

## 9. Audit Trail *(DEFERRED — blocked on JC.Core `EntityKey`)*

Planned in full so it's ready when the column lands.

### 9.1 The unblocking change
`AuditEntry` today is `{ Action, AuditDate, UserId, UserName, TableName, ActionData(JSON) }` — **no
queryable record id**, so per-record history is impossible (the PK only lives in the create entry's
JSON). John is adding **`EntityKey`** to JC.Core's `AuditEntry`: a **comma-separated list** of the
affected record's primary-key value(s) (composite keys → `"42,7"`). With it, per-record grouping is a
plain `WHERE TableName=… AND EntityKey=…`.

### 9.2 Data trail (grouped by table)
- **Tables** — distinct `TableName` from `AuditEntries`, with counts.
- **A table → its records** — distinct `EntityKey`, paginated; each row links to that record's history.
- **A record → its history** — all entries for `(TableName, EntityKey)`, date-ordered: who
  **created / updated / soft-deleted / restored** it, with the `ActionData` diff rendered.
- **Soft-deleted records in a separate table** — records whose current state is soft-deleted
  (latest entry is `SoftDelete` with no later `Restore`), shown apart from active records.
- **Everything paginated** (`ToPagedListAsync`).

### 9.3 User trail (grouped by user) — *already supported, no column needed*
- **Users + System** — every actor, including **System** (`UserId == IUserInfo.SYSTEM_USER_ID` =
  `"System__ID"`), paginated.
- **A user → their actions** — all their `AuditEntries` across **every** table, **date-ordered**,
  paginated (`WHERE UserId=… ORDER BY AuditDate DESC`).

### 9.4 Retention
`AuditEntries` are pruned by JC.Core's existing `AuditCleanupJob` (retention months + per-table
minimums). Nothing new needed.

---

## 10. Logs *(DEFERRED — read-only viewers)*

> Doing the whole admin area in one shot would be unwise; this section is **planned, not built yet**.
> All are **read-only**, paginated, and cleared by background jobs.

- **Admin actions** (`AdminActionLog`, §5) — buildable as soon as wanted (the log accrues from Phase
  1); a paginated viewer filtered by actor / action / target. **Build-ready** but grouped here.
- **Notifications** (`Notification` + `NotificationLog`, `NotificationService.GetNotifications`
  paginated) — **meaningful now** (friend requests use notifications).
- **Email** (`EmailLog` + recipient/sent/content `LogModel`s on `IEmailDbContext`) — **empty until
  A1** (email isn't actually sent until the release-gate tenant is provisioned). Build alongside A1.
- **Messaging** (`ChatThread`/`ChatMessage` + `ThreadActivityLog`/`MessageReadLog`) — **nothing to
  show until E1** (messaging isn't built). Build alongside E1.

Each comms package brings its own log-cleanup; `AuditEntries` use `AuditCleanupJob`; `AdminActionLog`
needs a small retention job (or rides a shared one).

---

## 11. Cross-Cutting Notes

- **Pagination** is universal — `PagedList<T>` / `ToPagedListAsync` (already used in
  `BlockAndReportService` and the profile page).
- **Admin-action auditing** is the `AdminActionLog`, *not* the JC.Core audit trail (which never sees
  these file/Identity changes).
- **Config files** (`config/rules/*.json`) are the settled pattern for admin-editable, file-backed
  config (rules, turn tax, settings) — each behind a singleton service with `Import()` + `Save()`.
- **Don't `RefreshSignInAsync` a *target* user** — only the current request's own user (§6.2).

---

## 12. New Pieces (inventory)

| Piece | Kind | Where |
|---|---|---|
| `Areas/Admin/*` + `AdminArea` / `SystemAdminOnly` policies | UI + auth | web |
| `AdminActionLog` + enums | `LogModel` entity (+ migration) | web |
| `IPendingClaimsRefresh` + refresh middleware | service + middleware | web |
| `OrphanCleanupJob`, `GameRetentionJob` | Hangfire jobs | web |
| `SettingsService` + `config/rules/settings.json` | service + file | web |
| `TurnTaxService.Save(...)` | method | web |
| `ReportResolution` flags + property | enum + `ReportedUser` field (+ migration) | web |
| Reports list query; admin game-list query; admin declare-draw | queries/methods | web |
| Extend `TryDeleteGame` to allow `Finished` | tweak | web |
| `AuditEntry.EntityKey` | **JC.Core** column | package (John) |
| Audit-trail pages; log viewers | UI | web *(deferred)* |

---

## 13. Phased Build

Each phase is a self-contained slice (and a section of this doc):

1. **Shell** — `Areas/Admin`, auth policies, layout + nav, `AdminActionLog` (model + migration; every
   later handler writes to it).
2. **User Management** — list/roles/disable/delete + the refresh registry/middleware + `OrphanCleanupJob`.
3. **Reports** — `ReportResolution` (+ migration), queue query, quick actions → log.
4. **Game Management** — `settings.json` + `SettingsService` + `GameRetentionJob`; admin game list +
   state-gated actions (+ declare-draw, + `TryDeleteGame` Finished).
5. **Rules + Turn Tax** — editors over `RuleCatalog.TryUpdateRules` + `TurnTaxService.Save`.
6. **Audit Trail** *(deferred)* — once `AuditEntry.EntityKey` lands (§9).
7. **Logs** *(deferred)* — `AdminActionLog` viewer + Notifications now; Email with A1; Messaging with E1 (§10).

---

## 14. Open / To Confirm

- **Resolution scope (§7.1)** — applying a report action to **all** the reported user's open reports
  (assumed) vs only the one acted on.
- **`AdminActionLog` retention** — its own small job vs folding into a shared log-retention job.
- **Re-enable / lift-Restricted by Admin** — included as the non-destructive inverse of Admin's
  allowed actions; confirm that's intended.

---

## 15. Traceability

- **`v1-roadmap.md` §5 (C1)** — the roadmap entry this realises; it flagged the need for this doc.
- **`docs/pckg-docs/JC.Core`** — audit trail (`AuditEntry`, `AuditAction`, `AuditCleanupJob`),
  `LogModel`/`AuditModel`, `PagedList`/`ToPagedListAsync`, `IUserInfo` (`SYSTEM_USER_ID`).
- **`docs/pckg-docs/JC.Identity`** — `UserManager`/`RoleManager`, `IsEnabled`, role seeding
  (`ConfigureAdminAndRolesAsync`, `SystemRoles.GetAllRoles`).
- **`docs/pckg-docs/JC.Communication`** — email / messaging / notification log entities (§10).
- **Code seams** — `Services/Games/GameService.cs` (`TryCancelGame`/`TryDeleteGame`/`ForceRefresh`),
  `Services/ProfileService.cs` (role toggles), `Services/Friends/BlockAndReportService.cs`
  (`ReportedUser`), `Services/RuleCatalog.cs` (`TryUpdateRules`),
  `Services/GameEngine/TurnTaxService.cs`, `Authorization/HangfireDashboardAuthFilter.cs`,
  `Data/AppRoles.cs`, `Program.cs` (FallbackPolicy + role seeding).
</content>
</invoke>
