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
       (email, roles)            (names, blocking)        (the big build)
                                                              │
                              D. Gameplay & stats   ←─────────┘
                              E. Optional enhancements (any time after their deps)
```

- **A first** because roles + verified email are the substrate several later items lean on (the
  `Restricted` role gates social/game actions; admin needs roles; moderation needs an audit trail
  that already exists in JC.Core).
- **B before launch** — names and blocking are the user-visible safety surface.
- **C is the largest single piece** — it's where moderation is actually performed, so it follows
  the role model.
- **D & E** are gameplay/feature polish that can slot in once their dependencies exist.

**Priority key:** 🔴 must-have for V1 · 🟡 optional / stretch.

| # | Item | Phase | Priority | Leans on |
|---|---|---|---|---|
| A1 | Email confirmation (tenant + app registration) | A | 🔴 | JC.Identity, JC.Communication.Email |
| A2 | Roles + the `Restricted` level | A | 🔴 | JC.Identity |
| B1 | Username & display-name validation + profanity filter | B | 🔴 | A2 |
| B2 | Friends / social blocking revisit | B | 🔴 | — |
| C1 | Admin area — full stack | C | 🔴 | A2, JC.Core audit |
| D1 | Most-landed-space frequency stat | D | 🔴 | — |
| D2 | Money cap & turn tax (anti-snowball) | D | 🟡 | — |
| E1 | Friend messaging + game invites | E | 🟡 | B2, JC.Communication |
| E2 | Hidden profile / soft-block | E | 🟡 | B2 |
| E3 | Cards reference page | E | 🟡 | — |

---

## 3. Phase A — Identity foundations

### A1. Email confirmation 🔴
**What.** Stand up a **Microsoft Entra tenant + app registration** for outbound email, wire it into
the app's email sender (JC.Communication.Email), and **enable the ConfirmEmail flow** in the
register / ASP.NET Identity pages (currently disabled).

**Why.** A public app needs verified email — for account recovery, to deter throwaway/abuse
accounts, and as the baseline trust signal. It's also an external prerequisite (tenant + app
registration provisioning), so it's worth starting early even though the code change is small.

**Scope.**
- Provision the tenant + app registration; obtain client id / secret (or certificate).
- Configure the email sender against it. **Secrets live in env vars / user secrets, never committed
  appsettings** (see config policy).
- Turn on `RequireConfirmedAccount` (or equivalent) and un-stub the ConfirmEmail Identity pages;
  ensure the confirmation email is sent on register and on email change.

**Open questions.**
- Graph API send vs SMTP relay — which does the tenant/app-registration support cleanly?
- Grace behaviour: can an unconfirmed account do anything, or is it fully gated until confirmed?

### A2. Roles & the `Restricted` level 🔴
**What.** Lean on JC.Identity's role system (`AppRole : BaseRole`, `RoleManager`, the
`SystemRoles`-derived roles class seeded by `ConfigureAdminAndRolesAsync`) and add the main new
application role: **`Restricted`**.

**Why.** `Restricted` is the moderation lever — a soft penalty short of a ban — and the role system
underpins the admin area (C1) and several gated actions. Defining it early lets later features
check it as they're built.

**Scope.**
- Add `Restricted` (and confirm the full app role set) to the app's `SystemRoles`-derived class so
  it's seeded.
- Thread role checks into the gated actions (see open question for the boundary).

**Open question — the `Restricted` boundary (needs discussion).** Candidate restrictions, to be
confirmed: **no sending friend requests**; possibly **no creating games or board skins**; possibly
**no joining games at all**. The exact line is a product decision — it should be enough to neutralise
a bad actor without silently breaking a borderline account. Each restriction needs a clear, friendly
"you can't do this because your account is restricted" surface, not a silent failure.

---

## 4. Phase B — Content safety

### B1. Username & display-name validation + profanity filter 🔴
**What.** Two parts:
1. **Display names** — a new user-settable, user-**changeable** field (distinct from the login
   username). Users can set and later change their display name.
2. **Validation + profanity filtering** on **both** usernames (at register) and display names (set /
   change) — reject explicit / sensitive / rude words and phrases.

**Why.** Names are the most visible user-authored content (leaderboard, profiles, in-game). For a
public launch they must be moderated up front, not after the fact.

**Scope.**
- Display-name field on the user/profile model + the set/change UI; uniqueness is *not* required
  (display names can collide; usernames stay unique).
- A filtering layer applied to both: structural validation (length, allowed charset) **plus** a
  profanity/blocklist check that normalises common evasions (case, leetspeak `a→@/4`, separators,
  repeated chars) before matching, and catches multi-word phrases.
- Apply on every write path: register, display-name set/change, and (defensively) any admin edit.

**Open questions.**
- Blocklist source — a curated list vs a library/service. Self-hosted list keeps it offline and
  tweakable; weigh maintenance vs coverage.
- False-positive handling (the "Scunthorpe problem") — tune toward fewer false rejects, with admin
  override.

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

### E2. Hidden profile / soft-block 🟡
**What.** A profile toggle that **hides you from the public leaderboard** — you appear as
"Unknown" there — while you can still add and be added as a friend. A **soft-block** on visibility,
not interaction.

**Why.** Privacy for users who want to play without being publicly ranked.

**Scope.** A `HiddenProfile` flag on the user; the leaderboard excludes hidden users from the public
ranking (or renders them as "Unknown"); friends-facing views still show them normally. Decide whether
hidden users still appear in friends' compare views (likely yes — it's friends-only there).

### E3. Cards reference page 🟡
**What.** A public page listing **every card** with a description of **what it does in-game**.

**Why.** Player-facing reference / discoverability for the card system.

**Scope — important data-modelling note.** Add a **`Description` field to the persisted card-ID DB
record** (`PersistedCardIds`), populated **on each import**. **Do NOT add a description to
`CardModel`** — the description lives **only** in the DB record (and the JSON source), never on the
in-game/snapshot model. This keeps the snapshot lean and avoids the description riding every per-turn
working copy; the page reads descriptions straight from the DB records.

---

## 8. Out of scope for V1 (noted, deferred)

- **NOPE cards** — the universal chainable counter (`cards-design.md` §6) remains a post-V1 card
  feature; immunity (its one-shot cousin) is done.
- **R-08 orchestrator/dice test harness** — the suspected-but-unconfirmed dice-modifier quirks
  (see `docs/review.md` R-08) need a unit/simulation test pass; not a V1 blocker but the natural
  next engine-hardening work.

---

## 9. Related docs

- `design-docs/game-design/game-engine.md`, `game-rules.md` — the engine D2 (money cap/turn tax)
  changes against (lockstep with the rules doc + tests).
- `design-docs/game-design/game-stats.md` — where D1's new stat slots in.
- `design-docs/cards/` — the card system E3 surfaces and D2 must not destabilise.
- `docs/pckg-docs/JC.Identity` (roles, email confirm), `JC.Communication` (Email, Messaging,
  Notifications), `JC.Core` (audit trail) — the package capabilities A1/A2/C1/E1 build on.
- `docs/review.md` — the code review whose fixes preceded this roadmap; R-08 is the carried-over
  open thread.