# C1 — Admin & Moderation Area

The operational backbone for running the app in public: the admin UI + services that let
SystemAdmins/Admins **see what's happening and act on it**. This is the single largest V1 piece
(roadmap C1), so it gets its own doc and a **phased build** — not one shot.

**Status:** design. Phased (see §13). Phases 1–5 are built. The **Audit Trail** (§9) is **now unblocked** —
the JC.Core `AuditEntry.EntityKey` column has landed — and is the active next phase, alongside the
**log viewers** (§10 — admin/comms logs, several gated on A1/E1) and the reusable **Recent Activity panel**
(§7.3) those feed.

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

### 7.3 Recent Activity panel — a reusable composition

The Report-Details "recent activity" mini-dashboard (the placeholder under the report info) is built as a
**reusable, user-keyed panel** — a composition of independent activity streams, each a reusable atom owned
by its own area (Audit §9, Logs §10, Games §8). It surfaces on **Reports → Details** (the reported user)
first, and is reusable on **User → Details** and anywhere a per-user activity view is wanted; nothing here
is bespoke to Reports — the reported user is just a user.

**The reusable-atom contract** (generalises the `_UsersTable` + `FullTable` pattern). Every stream exposes:
- A **scoped query** bound to a user id (games by host **or** player; audit `WHERE UserId`; admin-log
  `WHERE CreatedById`; comms by recipient), returning a `PagedList<T>` (the dedicated full page) or a
  page-sized preview (the panel).
- A **reusable table partial** — the *same* partial the stream's full page renders, populated from a list
  of items (mirrors how `_UsersTable` / `_GamesTable` / etc. are model-driven). The panel just hands each
  partial its scoped, page-sized list — it owns no queries of its own.

**The streams, source, auth gate, and preview size:**

| Stream | Source (scoped to user X) | Visible to | Size |
|---|---|---|---|
| Message logs | Messaging logs for X | Admin or SystemAdmin — *empty until E1* | 15 |
| Recent games | `GameManagementService` — games where X is **host OR player** (`hostIdSearch` = `playerIdSearch` = X) | **SystemAdmin only** | 10 |
| Admin logs | `AdminActionLog` where X is the **acting admin** (`CreatedById = X`) | Admin or SystemAdmin — **and** only when X **has ≥1 entry OR currently holds Admin/SystemAdmin** | 5 |
| Email logs | `EmailLog` (recipient X) | Admin or SystemAdmin — *empty until A1* | 5 |
| Notification logs | `Notification` / `NotificationLog` (recipient X) | Admin or SystemAdmin | 5 |
| User trail | `AuditEntries WHERE UserId = X` (X's own audited actions, §9.3) | Admin or SystemAdmin | 30 |

- **Auth split (important).** Report management is **Admin *or* SystemAdmin**, but **game management is
  SystemAdmin-only** — so the **Recent games** stream renders **only when the viewing admin is a
  SystemAdmin**. Every other stream is visible to any admin-level role.
- **Admin-logs visibility rule.** Show the section when the viewed user **currently holds Admin/SystemAdmin
  OR has ≥1 admin-log entry** — a user may have been an admin previously; if they aren't now but have
  historical action logs (as the actor), those still surface. *(`CreatedById = X` = the actions X took as an
  admin; moderation-history "actions taken *against* X" via `TargetId` is a distinct possible stream, not
  included here.)*
- **Gated streams render but stay empty** until their backing feature lands (Email → A1, Message → E1),
  consistent with §10.

**Layout** (the panel's internal structure; its placement inside Reports/User Details is a UI concern):
- **Two 50/50 columns**
  - **Left:** Message logs (15) · Recent games (10, *SystemAdmin only*)
  - **Right:** Admin logs (5, *gated*) · Email logs (5) · Notification logs (5)
- **Full-width below:** User trail (30)

**Build note.** The panel lands incrementally — each stream appears as its backing atom is built. Recent
games, notifications, admin logs, and the user trail are buildable now; email/message fill in with A1/E1.
Reuse demands the atoms come first: build each stream's scoped query + table partial (§8/§9/§10), then the
panel and the standalone pages both compose them.

### 7.4 Build plan — **all atoms now exist** (log viewers + audit trail are built)

The log viewers (§10) and audit trail (§9) shipped, so every stream's scoped query **and** reusable table
partial already exist. This is now a **composition** job, not new queries. Confirmed inventory (each stream =
existing service method + existing partial + model, scoped to user X):

| Stream | Fetch (scoped to X) | Partial | Model | Size |
|---|---|---|---|---|
| Message logs | `AppLogService.GetThreadActivityLogs(p, 15, search: X)` | `Logs/Messaging/_ThreadActivityLogsTable` | `ThreadActivityLogTableModel` | 15 |
| Recent games *(SysAdmin)* | `GameManagementService.GetGames(p, 10, host: X, player: X)` | `Games/_GamesTable` | `GameTableModel` | 10 |
| Admin logs *(gated)* | `AppLogService.GetAdminLogs(p, 5, search: X)` | `Logs/Admin/_AdminLogsTable` | `AdminLogTableModel` | 5 |
| Email logs | `AppLogService.GetEmailLogs(p, 5, search: X)` | `Logs/Email/_EmailLogsTable` | `EmailLogTableModel` | 5 |
| Notifications | `AppLogService.GetUserNotifications(X, p, 5, …)` | `Logs/Notifications/_NotificationsTable` | `NotificationTableModel` | 5 |
| User trail | `AuditTrailService.GetUserTrail(X, p, 30)` | `Audit/_AuditTable` (`IsUserTrail`) | `AuditTableModel` | 30 |

**Pattern:** *search by user id* for message / admin / email logs; *explicit per-user functions* for games /
notifications / user-trail. **Notifications = the per-user `_NotificationsTable`** (via `GetUserNotifications`),
**not** the read/unread `_NotificationLogsTable`.

**Decisions (confirmed):**

1. **Preview mode (drop pagination + "View all").** Add a `Preview` bool to each of the 6 `TableModel`s. When
   set, the partial **omits its `<pagination>`** (and count header); the panel renders a section header with a
   **"View all →"** link to the standalone page, pre-scoped to X:
   - Message logs → `/Admin/Logs/Messaging/Index?Search={X}`
   - Recent games → `/Admin/Games/Index?Search={X}` — the games-list `search` matches
     `Players.Any(p => p.UserId.Contains(search))`, so search-by-id works (SysAdmin-only page).
   - Admin logs → `/Admin/Logs/Admin/Index?Search={X}`
   - Email logs → `/Admin/Logs/Email/Index?Search={X}`
   - Notifications → `/Admin/Logs/Notifications/User/{X}`
   - User trail → `/Admin/Audit/Users/Trail/{X}`
2. **Placement = a fresh full-width row.** The WIP card is replaced by a **full-width block below** the
   report/actions row. The **50/50 split spans the full page width** (left: Message logs · Recent games;
   right: Admin logs · Email logs · Notifications); the **User trail is its own full-width row** beneath the
   50/50.
3. **Reusable = composed view model + partial + builder.** A `RecentActivityModel` holds each sub-`TableModel`
   (nullable when a stream is hidden) + the view-all URLs; `_RecentActivityPanel.cshtml` renders the layout and
   calls each sub-partial in preview mode. A small **builder** (e.g. `RecentActivityService.Build(userId,
   viewerIsSystemAdmin, userHoldsAdminRole)`) injects `AppLogService` + `GameManagementService` +
   `AuditTrailService` and composes the scoped queries — so Reports → Details **and** User → Details both just
   call the builder (the panel owns no queries).
4. **Auth / gates.**
   - **Recent games — SysAdmin only.** `GameManagementService.GetGames` calls `AuthCheck()` (throws if not
     SysAdmin) **in the method, not the ctor** — so inject freely, but only **call** it when the viewer is a
     SystemAdmin; otherwise leave the games stream null (hidden).
   - **Admin logs — gated.** Render only when X **currently holds Admin/SystemAdmin OR has ≥1 admin-log entry**
     (`CreatedById = X`); else omit the section.
   - `AppLogService` / `AuditTrailService` **guard their constructors** (throw if the resolver isn't Admin/
     SystemAdmin) — fine here, the panel is admin-only, but it's why the builder must run in an admin request
     context (cf. the GithubManager constructor-guard gotcha).

**Build steps:** (1) add `Preview` to the 6 `TableModel`s + the pagination-drop in each partial; (2)
`RecentActivityModel`; (3) `RecentActivityService.Build(...)`; (4) `_RecentActivityPanel.cshtml` (50/50 + full-
width trail; hide a cell when its sub-model is null); (5) wire into Reports → Details (pass `ReportedUserId`,
`IsSystemAdmin`, and whether the reported user holds admin) — reusable on User → Details next.

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
  state, **players (+ user ids)**, board, turn count, timestamps. The same query also serves a
  **per-user** scope (pass a user id as both `hostIdSearch` and `playerIdSearch` → games where they are
  **host OR player**), which is the **Recent games** atom the §7.3 activity panel reuses — SystemAdmin-only.
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

## 9. Audit Trail *(unblocked — `EntityKey` has landed)*

### 9.1 The unblocking change *(done)*
`AuditEntry` was `{ Action, AuditDate, UserId, UserName, TableName, ActionData(JSON) }` — **no
queryable record id**, so per-record history was impossible (the PK only lived in the create entry's
JSON). JC.Core's `AuditEntry` now has **`EntityKey`**: a **comma-separated list** of the affected record's
primary-key value(s) (composite keys → `"42,7"`). With it, per-record grouping is a plain
`WHERE TableName=… AND EntityKey=…`. *(Shipped as `varchar(512)` so the `(TableName, EntityKey)` index
stays within MySQL's 3072-byte limit.)*

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
6. **Audit Trail** — `AuditEntry.EntityKey` has landed, so the Data trail is unblocked (§9). Yields the
   **User-trail atom** the §7.3 panel consumes.
7. **Logs** — `AdminActionLog` viewer + Notifications now; Email with A1; Messaging with E1 (§10). Each
   viewer is a **reusable atom** (scoped query + table partial) the §7.3 Recent Activity panel composes.
8. **Recent Activity panel** (§7.3) — assemble the per-user composition once its atoms (User trail,
   AdminActionLog, Notifications, Recent games) exist; drop it into Reports → Details (and User → Details).

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
