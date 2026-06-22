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
| B2 | Friends / social blocking revisit | B | 🔴 | — |
| C1 | Admin area — full stack | C | 🔴 | A2, JC.Core audit |
| D1 | Most-landed-space frequency stat | D | 🔴 | — |
| D2 | Money cap & turn tax (anti-snowball) | D | 🟡 | — |
| E1 | Friend messaging + game invites | E | 🟡 | B2, JC.Communication |
| E2 | Hidden profile / soft-block | E | 🟡 ◐ | B2 |
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
- `Profanity.Detector` is **case-sensitive** (lowercase list) — input is lowercased before the library
  call. The library check on the *normalised* (uppercase) string still needs lowercasing to catch
  leet-evasions of common words via the library (small open fix).
- Admin-edit write path is deferred to **C1** (no admin edit surface exists yet).

### B2. Friends / social blocking revisit 🔴
**What.** A final hardening pass over the friends/social system, focused on **blocking**.

**Why.** Blocking is a safety primitive; it must be airtight before a public launch.

**Scope.**
- **Audit every surface** for block-respect: profile view, leaderboard/compare, friend search,
  friend lists, and (if built) messaging/invites — a block must hide and prevent interaction in
  both directions.
- **A block clears pending friend requests** between the two users (both directions), and prevents
  new ones while the block stands.
- Confirm the request lifecycle (send / accept / decline / cancel / remove) has no gaps the block
  pass should close.

---

## 5. Phase C — Admin & moderation

### C1. Admin area — full stack 🔴
**What.** The entire admin UI + services stack. This is the **single largest piece of V1 work** and
the operational backbone for running a public app.

**Why.** Once real users are on the app, it has to be *operable*: see what's happening and act on
it. JC.Core already writes a full **audit trail** (`AuditEntries` — user id, table, action,
timestamp, JSON snapshot), so the read/operate surface is what's missing.

**Scope (break into sub-areas, each its own slice).**
- **Audit-trail viewing** — browse/filter `AuditEntries` (by user, entity, action, time).
- **Analytics** — app/usage dashboards (signups, active games, completions, etc.).
- **User management** — view users, assign/remove roles (notably apply/lift `Restricted` from A2),
  enable/disable accounts, edit moderated fields.
- **Game management** — inspect, cancel, or remove games (the host controls already exist as a
  starting seam).
- **Board management** — review/moderate board skins (names are user-authored → moderation target).
- Gated to the `Admin` / `SystemAdmin` roles; every admin action is itself audited (it already is,
  via JC.Core).

**Note.** This warrants its **own design doc** once started — it's big enough that planning it
inline here would unbalance this roadmap. This entry just fixes its place and dependencies.

---

## 6. Phase D — Gameplay & stats

### D1. Most-landed-space frequency stat 🔴
**What.** Alongside `MostLandedOnBoardIndex`, store and show **how many times** the player landed on
that most-landed space.

**Why.** Small, completes an existing stat — "your most-landed space" reads oddly without the count.

**Scope.**
- The count is already computed during the movement projection (the max value of the land-on
  tally). Add a `MostLandedOnBoardIndexCount` (or similar) to the stat record + a migration, and
  surface it on the stat catalogue / render alongside the existing field. Define its cross-game
  aggregation (avg/total/min/max) consistently with the other numeric stats.

### D2. Money cap & turn tax (anti-snowball) 🟡
**What.** Cap player cash (proposed **£20,000**) to stop runaway leaders, and optionally a **global
turn tax** — e.g. **20% on anything above £5,000** at a turn boundary — to bleed off hoarded cash.

**Why.** Prevents the snowball that drags games past 10 hours; a hard ceiling + a soft drain keeps
games finite and competitive. Directly improves first-session UX.

**Open questions (a balance discussion — flagged optional for that reason).**
- Is a hard £20K cap right, or does it create weird edge cases (e.g. a sale that would exceed it)?
- Turn-tax mechanics: threshold (£5K?), rate (20%?), where it's paid (bank? Free Parking?), and
  which turn boundary it fires on. Needs playtesting, and it touches the turn loop + a new rule
  citation, so it's a deliberate rules change (lockstep with `game-rules.md`), not a quick tweak.

---

## 7. Phase E — Optional enhancements

### E1. Friend messaging + game invites 🟡
**What.** Use **JC.Communication.Messaging** to let **friends** send each other messages, and
potentially an **invite-to-game** feature via **JC.Communication.Notifications**.

**Why.** Rounds out the social layer; game invites smooth the "play with a friend" flow.

**Scope / guards.** Messaging is **friends-only** and must respect **blocking** (B2) and the
`Restricted` role (A2). Game invites ride the notifications channel. Confirm the JC.Communication
Messaging/Notifications APIs against pckg-docs when picked up.

### E2. Hidden profile / soft-block 🟡 — ◐ **PARTIAL** (most of it landed off A2's role infra)
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

**Remaining.**
- The hide/show toggle now lives on the **Account tab** (`Manage`, via `TryHideUser` /
  `TryUnhideUser`). Still to do: add the **same toggle to `/Profile/Index`** so it's reachable from
  the profile page too.

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