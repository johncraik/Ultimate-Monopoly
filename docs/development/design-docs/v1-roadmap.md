# Road to V1 — Release Roadmap

The plan that takes the app from its current state (a working game engine + social/profile
platform) to a **publicly launchable V1**. It is a planning doc, not an implementation spec: it
names the work, orders it sensibly, and records the key decisions and open questions for each
piece — the detailed design for the larger items will get its own doc as it is built.

**Status:** planning. Authoritative for *what V1 needs and in what order*; the per-item specifics
(role boundaries, profanity strategy, money-cap numbers, admin sub-areas) are flagged as open and
settled with the developer as each is picked up.

---

## 1. What "V1" means

V1 is the first version safe to put in front of the **general public** — not just trusted
playtesters. That bar adds three things the current build doesn't fully have:

1. **Trust & safety** — verified accounts, moderated user-visible content (names), and social
   controls (blocking) that are actually airtight.
2. **Operability** — an admin/moderation surface to see what's happening (audit trail, analytics)
   and act on it (manage users, games, boards).
3. **Good first-session UX** — games that finish in a reasonable time, and the small stat/info
   polish that makes the app feel complete.

Everything below serves one of those three. Items the developer flagged **optional** are stretch
goals — valuable, but V1 can ship without them.

---

## 2. Ordering & priorities

The items were collected unordered; this is the suggested build order, grouped into phases by
dependency and risk. The arrows are *soft* dependencies, not hard blocks.

```
A. Identity foundations   →   B. Content safety   →   C. Admin & moderation
       (roles)                   (names, blocking)        (the big build)
                                                              │
                              D. Gameplay & stats   ←─────────┘
                              E. Optional enhancements (any time after their deps)

      ── then, immediately before launch ──────────────────────────────────
      R. Release gate — A1 email confirmation (the one paid external prereq)
```

- **A first** because roles are the substrate several later items lean on (the `Restricted` role
  gates social/game actions; admin needs roles; moderation needs an audit trail that already exists
  in JC.Core). *(Email confirmation — formerly A1 in this phase — has moved to the release gate R;
  see below.)*
- **B before launch** — names and blocking are the user-visible safety surface.
- **C is the largest single piece** — it's where moderation is actually performed, so it follows
  the role model.
- **D & E** are gameplay/feature polish that can slot in once their dependencies exist.
- **R last** — email confirmation (A1) needs a **paid** Microsoft Entra tenant / app-registration
  licence to send mail. There's no point provisioning (and paying for) it before we're actually
  ready to ship, so it is deliberately the **final step before launch**, not a foundation. The code
  change itself is small; only the external paid prerequisite gates it.

**Priority key:** 🔴 must-have for V1 · 🟠 must-have, but deliberately **last** (an external *paid*
prerequisite — provisioned only at release) · 🟡 optional / stretch · 🟢 **implemented** · ◐ **partially implemented**.

| # | Item | Phase | Priority | Leans on |
|---|---|---|---|---|
| A2 | Roles + the `Restricted` level | A | 🟢 | JC.Identity |
| B1 | Username & display-name validation + profanity filter | B | 🟢 | A2 |
| B2 | Friends / social blocking revisit | B | 🟢 | — |
| C1 | Admin area — full stack | C | 🔴 | A2, JC.Core audit |
| D1 | Most-landed-space frequency stat | D | 🟢 | — |
| D2 | Turn tax (anti-snowball) | D | 🟢 | — |
| E1 | Friend messaging + game invites | E | 🟡 | B2, JC.Communication |
| E2 | Hidden profile / soft-block | E | 🟢 | B2 |
| E3 | Cards reference page | E | 🟡 | — |
| A1 | Email confirmation (tenant + app registration) | R | 🟠 | JC.Identity, JC.Communication.Email |

---

## 3. Phase A — Identity foundations

### A2. Roles & the `Restricted` level 🟢 — ✅ **IMPLEMENTED**
**What.** Lean on JC.Identity's role system (`AppRole : BaseRole`, `RoleManager`, the
`SystemRoles`-derived roles class seeded by `ConfigureAdminAndRolesAsync`) and add the main new
application role: **`Restricted`**.

**Why.** `Restricted` is the moderation lever — a soft penalty short of a ban — and the role system
underpins the admin area (C1) and several gated actions.

**Boundary (decided).** A `Restricted` account **cannot**: send friend requests, send messages,
create games, or create / share board skins. Everything else stays open — it can still **accept /
decline / remove** friends, **join** games, **edit** its existing board skins, and **remove** its
existing board-skin shares. The line is "neutralise a bad actor's outward/creative actions without
breaking a borderline account." Recorded on `AppRoles.RestrictedDesc`.

**Implemented.**
- `AppRoles.Restricted` (+ `RestrictedDesc`) added and **seeded** automatically via
  `ConfigureAdminAndRolesAsync<AppUser, AppRole, AppDbContext, AppRoles>` (`Program.cs`) — discovered
  by JC.Identity's `{Name}` / `{Name}Desc` reflection convention.
- **Authoritative server-side guards** (UI hiding is defence-in-depth only, never the sole gate):
  - **Create game** — `GameSetupService.TryCreateNewGame` (+ `Pages/Games/Index.OnGetAsync` hides the UI).
  - **Send friend request** — `FriendService.TrySendFriendRequest` (accept / decline / remove stay open).
  - **Share board skin** — `BoardSkinShareService.TryShareBoardSkin` blocks *add / restore* but allows
    *remove*; `Pages/Boards/Share` surfaces a friendly "your account is restricted" message.
  - **Create board skin** — `BoardSkinService.TrySaveSkin` (create branch) (+ `Pages/Boards/Edit.OnGetAsync`
    hides the UI). Editing an existing skin is intentionally still allowed.
- **Send messages** is part of the declared boundary but has no surface yet — it is enforced when
  messaging lands (E1 already records the `Restricted` + blocking guards it must honour).

**Follow-up (minor, optional).** The create-board-skin guard returns a bare failure with no model
error, so a crafted-POST bypass shows a generic "could not save" message rather than a
restricted-specific one (the UI already hides create, so a normal user never reaches it). Add a
`modelState.AddModelError` line if message parity with the Share page is wanted.

---

## 4. Phase B — Content safety

### B1. Username & display-name validation + profanity filter 🟢 — ✅ **IMPLEMENTED**
**What.** Two parts: (1) a new user-settable, **changeable** display name (distinct from the login
username); (2) profanity filtering on **both** usernames (at register) and display names (set/change).

**Why.** Names are the most visible user-authored content (leaderboard, profiles, in-game) — moderated
up front, not after the fact.

**Implemented.**
- **Display name** — editable on the **Account tab** (`Manage`), distinct from the username,
  **collisions allowed** (not unique). Persisted to JC.Identity `BaseUser.DisplayName` + a
  `RefreshSignInAsync` so the `display_name` claim refreshes immediately.
- **Filter pipeline** (`ProfanityService.Check` → `ProfanityResult(IsProfane, MatchedTerm, Source)`):
  - **`ProfanityNormaliser`** (static helper) canonicalises input — strip diacritics, leet-map
    (`@→a`, `1→i`…), drop separators/whitespace (defeats `f.u.c.k` / `f u c k`), collapse runs of 3+
    repeats (`fuuuck→fuck`). The same transform produces `BlockedWord.NormalisedWord`, so both sides
    of a match are comparable.
  - **`Profanity.Detector`** library (≈1.7M downloads) for the comprehensive list + its own
    Scunthorpe allow-list, **plus** a DB-backed local list (`BlockedWord` table) for extra terms.
  - **Local list infra:** `BlockedWordImportService` seeds additively from `BlockedWords_FilePath`
    (one word/line, `#` comments, never deletes); `BlockedWordsCacheService` caches it (NeverRemove,
    hydrate-on-miss, SystemAdmin-gated `Invalidate`).
- **Write paths wired:** register (username) and display-name set/change. The user-facing message is
  **generic** ("this name isn't allowed"); `MatchedTerm`/`Source` go to the audit/log only.

**Decided.** Library + private local list (no separate allow-list table for V1 — lean on the
library's own); bias to **under-block** with admin override.

**Caveats / follow-ups.**
- `Profanity.Detector` is **case-sensitive** (lowercase list), so the service lowercases **both** library
  passes before calling it — the raw input (`ToLowerInvariant`) and the normalised form
  (`Normalise(...).ToLowerInvariant()`), the latter catching leet-evasions of common words. No
  outstanding case-sensitivity work.
- Admin-edit write path is deferred to **C1** (no admin edit surface exists yet).

### B2. Friends / social blocking revisit 🟢 — ✅ **IMPLEMENTED**
**What.** A final hardening pass over the friends/social system, focused on **blocking**.

**Why.** Blocking is a safety primitive; it must be airtight before a public launch.

**Implemented.**
- **Block-respect on every built surface, both directions** (the `BlockAndReportService` checks are
  symmetric):
  - Leaderboard anonymises blocked users to the "User" view-model (`LeaderboardService`) — not
    hidden, so ranks stay stable.
  - The Compare page **and** the `Friends/Profile` detailed-stat page are both **friends-only +
    block-gated** → `NotFound`. (The profile page previously had **no** friend/block gate at all —
    the main hole this pass closed.)
  - Add-friend-by-username already rejects a block with a deliberately ambiguous "no user exists"
    message (doesn't reveal the block).
  - The friends list and friend-request lists now also **defensively filter** blocked users, so a
    block is airtight even against any stale relationship/request row.
- **A block clears pending friend requests** between the two users (both directions), inside the
  block transaction (`ProcessBlockAndReport`), and **bars new ones** while the block stands
  (`TrySendFriendRequest`). `TryAcceptFriendRequest` also re-checks for a block, so a request that
  predates a block can't be accepted into a friendship.
- **Request lifecycle completed:** added **cancel outgoing request** (`FriendService.TryCancelFriendRequest`
  + the `Cancel` page handler/button) — the lifecycle is now send / accept / decline / cancel / remove.

**Decided.**
- The detailed-stat profile (`Friends/Profile`) is **friends-only** (not public) and block-gated —
  the same gate as the Compare page.
- A block **soft-deletes** the pending request rows (consistent with how it soft-deletes the friend
  relationship), rather than marking them declined.

**Deferred.** Messaging / game invites are **mocks** today (the `Friends/Chat` page is dummy data),
so they have no live block surface yet; the block / `Restricted` guards they must honour land with
**E1**, when messaging is actually built.

---

## 5. Phase C — Admin & moderation

### C1. Admin area — full stack 🔴 — 📐 **DESIGNED** (`c1-admin-area.md`)
**What.** The entire admin UI + services stack. The **single largest piece of V1 work** and the
operational backbone for running a public app.

**Why.** Once real users are on the app, it has to be *operable*: see what's happening and act on it.

**Design.** Settled in **`c1-admin-area.md`** — a new `Areas/Admin` under two-tier `Admin` /
`SystemAdmin` auth, built in **phases**:
1. **Shell** + auth + **`AdminActionLog`** — an immutable log of *every* admin action. (Correction to
   the old assumption: admin actions are **not** auto-audited — `AppUser`/`AppRole` aren't `AuditModel`
   and rules/config are files — so this log fills the gap, not JC.Core's audit trail.)
2. **User management** — roles / disable / delete; a singleton refresh-registry + middleware to
   propagate role changes to live sessions; a separate `OrphanCleanupJob` for the orphaned-by-id
   records a hard delete leaves.
3. **Reports** — a `ReportResolution` `[Flags]` enum on `ReportedUser`; quick restrict / disable /
   delete actions on the reported user.
4. **Game management** — `config/rules/settings.json` retention + a `GameRetentionJob`; an admin game
   list with state-gated draw / cancel / delete.
5. **Rules + Turn Tax** editors — over `RuleCatalog.TryUpdateRules` + a new `TurnTaxService.Save`.
6. **Audit trail** — *deferred*, blocked on a JC.Core `AuditEntry.EntityKey` column (John) that
   unblocks the per-record data trail; the user trail is already supported.
7. **Log viewers** — *deferred* (read-only): the `AdminActionLog` page + notifications now, email with
   **A1**, messaging with **E1**.

See the doc for the full design, the auth matrix, and the open points.

---

## 6. Phase D — Gameplay & stats

### D1. Most-landed-space frequency stat 🟢 — ✅ **IMPLEMENTED**
**What.** Alongside `MostLandedOnBoardIndex`, store and show **how many times** the player landed on
that most-landed space.

**Why.** Small, completes an existing stat — "your most-landed space" reads oddly without the count.

**Implemented.**
- New `PlayerStatRecord.MostLandedOnBoardIndexCount` (`uint`). `PlayerGameStat` inherits the record,
  so it's a DB column there too (the copy-ctor copies it).
- `MovementStatsService` sets it from the max of the existing land-on tally (the same `MaxBy` that
  picks the index now also reads `.Value`); 0 in the guarded no-landings case (H-03).
- Cross-game aggregation in `PlayerStatRecord` is a **plain numeric** (avg/total → average, min/max →
  MinBy/MaxBy), mirroring `TimesLandedOnGo` — deliberately decoupled from the aggregated mode index
  (the "consistent with the other numeric stats" choice this item called for).
- Surfaced on the catalogue: the "Most landed-on space" tile/row carries a `Sub` note
  ("landed N times"), so both the single-game and comparison views show it with no layout change.

**Remaining.** Scaffold + apply the EF migration for the new `MostLandedOnBoardIndexCount` column
(the repo's global `dotnet ef` is 5.0.17, too old for net9 — add it from the IDE, e.g.
`Add-Migration MostLandedOnBoardIndexCount`). Stats fill in on the next recompute; pre-existing rows
read 0 until rebuilt.

### D2. Turn tax (anti-snowball) 🟢 — ✅ **IMPLEMENTED**
**What.** A **global turn tax** that bleeds off hoarded cash to stop runaway leaders. (The originally-
paired hard cash cap was **dropped** — see below.)

**Why.** Prevents the snowball that drags games past 10 hours; a soft, escalating drain on idle cash
keeps games finite and competitive without a blunt ceiling. Directly improves first-session UX.

**Implemented.**
- A **progressive, stacking** wealth tax (`TurnTax` model + `ITurnTaxService` / `TurnTaxService`),
  applied at the **start of each player's turn, before they roll** (top of
  `PlayerTurnOrchestrator.StartPlayerTurn`), on the **cash** in the player's account → **bank**
  (removed from the game) via `TransactionService.PayTurnTax` (`FinancialReason.TurnTax`, no
  shortfall — paid from cash on hand only, so it never bankrupts).
- Each bracket's rate applies to the whole amount above its threshold, and the brackets **stack**
  (default 10% / 30% / 50% over £5k / £10k / £20k → a £25,000 balance pays £9,000).
- **Global config** (`config/rules/turnTax.json`, loaded once at startup via `ITurnTaxService.Import()`)
  — chosen over a per-game snapshot for simplicity; disabled when every bracket is zero. Trade-off:
  a config change isn't pinned per game (it affects in-progress games' future turns).
- Rules lockstep: new `FinancialReason.TurnTax`, `RuleCode.TurnTax_Pay` / `TurnTax_Spend`, `rules.json`
  entries (admin-editable, so the description can track the configured brackets), and a `game-rules.md`
  "Turn Tax" section. Taxed on **every** turn-start (incl. extra rolls); only cash is taxed, so spending
  before the roll lowers the bill (cited as `TurnTax_Spend`).

- **Stat:** a `SpentOnTurnTax` spending stat wired full-stack (compute → aggregate → `PlayerGameStat`
  → catalogue), gated on `ITurnTaxService.Enabled` so the row/card only shows when the tax is on.

**Dropped — money cap.** The originally-proposed **£20,000 cash ceiling** was cut: on reflection a hard
cap is a blunt, unsatisfying way to curb snowballing, and it spawns awkward edge cases (a sale or rent
that would breach it — cap at the receipt, or lose the excess?). The progressive turn tax does the job
better, so the cap is **out of scope** for V1.

---

## 7. Phase E — Optional enhancements

### E1. Friend messaging + game invites 🟡
**What.** Use **JC.Communication.Messaging** to let **friends** send each other messages, and
potentially an **invite-to-game** feature via **JC.Communication.Notifications**.

**Why.** Rounds out the social layer; game invites smooth the "play with a friend" flow.

**Scope / guards.** Messaging is **friends-only** and must respect **blocking** (B2) and the
`Restricted` role (A2). Game invites ride the notifications channel. Confirm the JC.Communication
Messaging/Notifications APIs against pckg-docs when picked up.

### E2. Hidden profile / soft-block 🟢 — ✅ **IMPLEMENTED**
**What.** A profile toggle that **hides you from the public leaderboard** — non-friends see you as
"Unknown" there — while you can still add and be added as a friend, and **you and your friends still
see you normally**. A **soft-block** on visibility, not interaction.

**Why.** Privacy for users who want to play without being publicly ranked.

**Implemented.**
- Modelled as a **role** (`AppRoles.HiddenUser`), **not** the originally-sketched `HiddenProfile`
  flag — reuses the seeded-role infra (no migration) and gives C1's admin role-management a toggle
  for free. Seeded via `ConfigureAdminAndRolesAsync<…, AppRoles>`.
- **Leaderboard hiding** (`LeaderboardService.GetLeaderboard` + `ProfileService.GetHiddenUserIds`):
  a hidden user renders as the anonymised "Unknown" view-model (W/L/D kept, so still ranked) **only
  to non-friends** — self and friends see them normally. Enforced by a fresh `UserRoles` DB query,
  not auth claims, so a toggle takes effect immediately (no cookie refresh).
- **Set / unset** (`ProfileService.TryHideUser` / `TryUnhideUser`): self-service for your own
  account, **SystemAdmin-gated** to act on another user (a moderation seam for C1); idempotent.

**Surfaced everywhere.** The hide/show toggle now lives on the **Account tab** (`Manage`), the
**Leaderboard** (your own row), and the **Profile page** (`/Profile/Index` → Statistics tab, above the
W/L/D summary) — all via `ProfileService.IsHidden` + `TryHideUser` / `TryUnhideUser`.

**Decided.** Hidden users **do** still appear normally in friends' views (leaderboard hide is
non-friends-only); this settles the old "do they show in friends' compare views?" open question — yes.

### E3. Cards reference page 🟡
**What.** A public page listing **every card** with a description of **what it does in-game**.

**Why.** Player-facing reference / discoverability for the card system.

**Scope — important data-modelling note.** Add a **`Description` field to the persisted card-ID DB
record** (`PersistedCardIds`), populated **on each import**. **Do NOT add a description to
`CardModel`** — the description lives **only** in the DB record (and the JSON source), never on the
in-game/snapshot model. This keeps the snapshot lean and avoids the description riding every per-turn
working copy; the page reads descriptions straight from the DB records.

---

## 8. Release gate — A1, done last

The one item deliberately sequenced **after** everything else, immediately before launch.

### A1. Email confirmation 🟠
**What.** Stand up a **Microsoft Entra tenant + app registration** for outbound email, wire it into
the app's email sender (JC.Communication.Email), and **enable the ConfirmEmail flow** in the
register / ASP.NET Identity pages (currently disabled).

**Why.** A public app needs verified email — for account recovery, to deter throwaway/abuse
accounts, and as the baseline trust signal.

**Why last (not a foundation).** Provisioning the Entra tenant / app registration requires **buying
a licence** — a real, recurring cost. There is no reason to pay for it before the app is actually
ready to ship, so it is the **final step before launch**, not an early one. The *code* change is
small (un-stub ConfirmEmail, turn on `RequireConfirmedAccount`, point the email sender at the
tenant); only the paid external prerequisite gates it, and that cost should land as late as possible.

**Scope.**
- Provision the tenant + app registration; obtain client id / secret (or certificate).
- Configure the email sender against it. **Secrets live in env vars / user secrets, never committed
  appsettings** (see config policy).
- Turn on `RequireConfirmedAccount` (or equivalent) and un-stub the ConfirmEmail Identity pages;
  ensure the confirmation email is sent on register and on email change.

**Open questions.**
- Graph API send vs SMTP relay — which does the tenant/app-registration support cleanly?
- Grace behaviour: can an unconfirmed account do anything, or is it fully gated until confirmed?
- Does any earlier item (B/C/D) assume a *confirmed* email? Only **roles** (A2) are a true
  dependency for later work — nothing built before R may hard-require confirmed email, or it would
  be untestable for the whole pre-release build.

---

## 9. Out of scope for V1 (noted, deferred)

- **NOPE cards** — the universal chainable counter (`cards-design.md` §6) remains a post-V1 card
  feature; immunity (its one-shot cousin) is done.
- **R-08 orchestrator/dice test harness** — the suspected-but-unconfirmed dice-modifier quirks
  (see `docs/review.md` R-08) need a unit/simulation test pass; not a V1 blocker but the natural
  next engine-hardening work.

---

## 10. Related docs

- `design-docs/game-design/game-engine.md`, `game-rules.md` — the engine D2 (money cap/turn tax)
  changes against (lockstep with the rules doc + tests).
- `design-docs/game-design/game-stats.md` — where D1's new stat slots in.
- `design-docs/cards/` — the card system E3 surfaces and D2 must not destabilise.
- `docs/pckg-docs/JC.Identity` (roles, email confirm), `JC.Communication` (Email, Messaging,
  Notifications), `JC.Core` (audit trail) — the package capabilities A1/A2/C1/E1 build on.
- `docs/review.md` — the code review whose fixes preceded this roadmap; R-08 is the carried-over
  open thread.