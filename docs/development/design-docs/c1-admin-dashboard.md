# C1 — Admin Dashboard (hub-and-spoke design)

The landing page of the admin area (`/Admin` → `Index`) plus a small set of **spoke** sub-dashboards.
A single mega-page would be slow, hard to tier-gate, and a wall of noise — so the dashboard is a **hub** of
a few key metrics per area, each linking to a focused **spoke** that owns that area's view.

> **Status:** design (build spec). Companion to `c1-admin-area.md` (the operational area this dashboard fronts).
> The admin area itself is built (phases 1–8); this is the cap on it. The full metric menu is the
> **Appendix** below — `*` marks the metrics picked for v1; this top half is the agreed structure.

---

## 1. Architecture — hub & spokes

- **Hub** = `/Admin/Index`. A **triage alert strip** + **one tile per spoke** (2–3 live KPIs + a
  "View → {spoke}" button). Every hub metric is `¢` cheap, so the landing page runs **live, no precompute**.
- **Spokes** (focused sub-dashboards), each tier-gated:

  | Spoke | Tier | Owns |
  |---|---|---|
  | **Users** | Admin+ | identity health, activity, account safety |
  | **Community** | Admin+ | social graph (friends/requests/blocks) **+ moderation** (reports/restrictions/blocked-words) |
  | **Games** | **SystemAdmin** | game lifecycle / health / storage |
  | **Audit** | Admin+ | JC.Core `AuditEntry` trends |
  | **App Logs** | Admin+ (Issues also GithubManager) | admin-action log + comms + issues |

- **Deliberately out of scope** (decided): player **leaderboard/rankings** (the public board owns those);
  **ops / background-job / retention health** (the existing **`/hangfire`** dashboard owns those — no ops
  widgets duplicated here); deep **economy / gameplay `$$` analytics** and **board/card content** (backlog —
  see Appendix).
- **No `DashboardMetricsJob` for v1** — every chosen metric is `¢` (counts/rates), computed live. (Revisit
  only if the `$$` economy backlog is ever pulled in.)

---

## 2. The Hub — `/Admin/Index`

Evolve the current static section cards into **live tiles**, with a triage strip on top.

**Alert strip** (only shows a badge when non-zero — "what needs me now"; all `¢`):
- **Open reports** (Admin+) · **Oldest unresolved report** SLA (Admin+)
- **GitHub sync failures** (`ReportSent == false`) (GithubManager/Admin+)
- **Locked-out / brute-force watch** (accounts with `AccessFailedCount > 0` or active lockout) (Admin+)

**Tiles** — each shows **3 KPIs + a trend graph** (Games: **2 graphs**) + a "View →" link:

| Tile | Tier | 3 KPIs | Graph(s) |
|---|---|---|---|
| Users | A | Total users · Active now · Disabled (+restricted) | Registrations over time |
| Community | A | Open reports · Active blocks · New friendships (7d) | Reports over time *(TBD)* |
| Games | **S** | Live games · Total games · Completion rate | 2 graphs *(TBD)* |
| Audit | A | Entries (24h) · Busiest table · System-actor % | Audit entries over time *(TBD)* |
| App Logs | A | Admin actions (24h) · Email failures · Issues open | Admin actions over time *(TBD)* |

Tiles render only for the viewer's tier (Admin never sees the Games tile). GithubManager is funnelled
straight to `/Admin/Logs/Issues` by the page filter, so they don't see the hub.

---

## 3. Spoke: Users  *(Admin+)*

**v1 (starred, all `¢ ✓` live):** total users · enabled vs disabled · email-confirmed rate · 2FA rate ·
locked-out accounts · `AccessFailedCount > 0` watch · admins/SystemAdmins/GithubManagers counts · restricted
users · avatar customisation (% colour / % image) · most-used avatar images · **active now** · days-since-last-login
histogram · dormant accounts (30/60/90d) · W/L/D totals · win-rate distribution.

**Phase 2 — needs new tracking (`⊕`):** new registrations over time · logins over time · DAU/MAU history ·
cohort retention. All require a user **registration timestamp** (verify JC.Identity `BaseUser` exposes one) and
a **daily active-users snapshot** table + job (we store only *latest* `LastActiveUtc`). Not v1-blocking.

*(Backlog: DAU/WAU/MAU point-in-time, stickiness, days-since-active hist, never-logged-in, funnels — Appendix §1.)*

---

## 4. Spoke: Community  *(Admin+)*  — social graph + moderation

Curate against Appendix §6 (Social) + §7 (Moderation). Proposed v1 (all `¢ ✓` unless noted):

- **Moderation:** open reports (Alert) · reports over time · reports by reason (Bar) · resolution rate (Gauge) ·
  oldest unresolved (Alert) · most-reported users (Top-N, `$`) · restricted-users count · blocked-word list size.
- **Social graph:** total active friendships · friendships over time · friends removed over time · pending
  friend requests · request acceptance rate (Gauge) · total active blocks · blocks over time · most-blocked
  users (Top-N, `$`).

*(Backlog: reason→action heat, request funnel, reciprocal blocks, friends-per-user hist, profanity-hit-rate
[needs a `⊕` tally] — Appendix §6/§7.)*

---

## 5. Spoke: Games  *(SystemAdmin only)*

**v1 (starred, `¢/$ ✓` live):** total games · games by state · live games now · games awaiting players ·
games created over time · games concluded over time · completion rate · cancellation rate · abandonment watch ·
outcome split (Winner vs Drawn) · players-per-game distribution · average players per game *(+ any further
§2 stars below the truncation point)*.

**Out of scope here:** job/retention/storage ops → `/hangfire`. Board-usage and the `$$` economy/gameplay
analytics → backlog (Appendix §2–§4).

---

## 6. Spoke: Audit  *(Admin+)*

Curate against Appendix §12. Proposed v1 (all `¢ ✓`): audit entries over time · by action type (Bar) ·
**by table** (Top-N, busiest tables) · system vs human vs unknown actor (Pie) · most-active actors (Top-N) ·
soft-delete vs restore balance · audit-table size/age. *(Backlog: bulk-change anomaly Alert.)*

---

## 7. Spoke: App Logs  *(Admin+; Issues also GithubManager)*

Curate against Appendix §9 (Comms) + §10 (Issues) + §11 (Admin-action log). Proposed v1:

- **Admin-action log:** actions over time · by action type (Bar) · by target type (Pie) · per-admin leaderboard
  (Top-N) · recent actions (Feed) · destructive-actions count.
- **Issues:** open vs closed · bugs vs suggestions · sync failures (Alert) · reporters contacted (the
  IssueContact flow) · top reporters.
- **Comms:** notifications created over time · read rate (Gauge) · by type. **Email** (sent / success-rate /
  failures) and **Messaging** (messages over time) are `⚠` empty until **A1** / **E1** — render but flag.

---

## 8. Build notes

- **Routing:** spoke pages under `/Admin/Dashboards/{Users,Community,Games,Audit,Logs}` (Community spans the
  Reports area + the FK-less social tables, so a dedicated `Dashboards` folder is cleaner than hanging each off
  an existing area). Hub stays at `/Admin/Index`. Reachable from the hub tiles; optionally add a "Dashboard"
  sub-link at the top of each sidebar group.
- **Service:** a `DashboardService` (or per-spoke methods) composing the cheap live queries from the existing
  area services; ctor-gated Admin/SystemAdmin; **tier-filter** the Games spoke + tile to SystemAdmin. Mirrors
  the area's service pattern.
- **Charts:** no chart lib in the project yet. `Line/Bar/Pie/Gauge/Hist` want a lightweight lib (**Chart.js**
  the default); KPIs/alerts/feeds need none — a **counters-and-alerts-first** cut is shippable before charts.
- **Live & cheap:** v1 needs **no** background job (everything `¢`). The `⊕` trackers (cohort/registrations/
  logins/DAU-MAU history) are the only infra investment, deferred to phase 2.

---

## 9. Decisions

**Resolved:**
- Hub-and-spoke (5 spokes), not one page.
- **Community** folds in moderation (reports/blocks/restrictions/blocked-words) — one safety+social view.
- **Ops/job/retention health is NOT in the dashboards** — the `/hangfire` dashboard owns it.
- **No leaderboard/rankings** — the public board owns those (aggregate W/L/D + win-rate *distribution* kept as
  population health).
- Deep economy/gameplay `$$` analytics + board/card content → **backlog**.
- v1 is all `¢` live → **no precompute job**.

**Open:**
- Spoke routing path (`/Admin/Dashboards/{Area}` proposed).
- Chart library (Chart.js vs counters-first).
- Which `⊕` trackers to invest in (cohort retention etc.) and whether `BaseUser` has a registration timestamp.
- Finalise the Community / Audit / App-Logs metric picks (star them in the Appendix).

---

# Appendix — full metric catalogue

_The exhaustive menu of everything derivable. `*` marks the metrics picked for v1 (above). Everything unmarked
is the backlog. The legend below explains the Widget / Tier / Cost / Data tags used throughout._

---

## 0. How to read this doc

Every metric carries four tags so you can curate by value, audience, and cost.

**Widget** — the shape it renders as:
`KPI` single number · `KPIΔ` number + trend delta vs prior window · `Bar` / `Pie` breakdown by category ·
`Line` / `Area` time-series · `Spark` inline sparkline · `Hist` distribution/histogram · `Heat` heatmap ·
`Top-N` leaderboard/ranking · `Feed` recent-activity table · `Gauge` 0–100% dial · `Funnel` stage conversion ·
`Scatter` correlation · `Board` board-space heatmap · `Alert` threshold badge.

**Tier** — who may see it (mirrors the §4 auth matrix in `c1-admin-area.md`):
`A` Admin **or** SystemAdmin · `S` SystemAdmin only (everything game/economy is SystemAdmin-gated today —
`GameManagementService` runs `AuthCheck()`; see §Decisions) · `G` GithubManager (Issues only) · `Self` the
viewing admin's own activity.

**Cost** — query expense:
`¢` cheap (indexed `COUNT`/`GROUP BY`) · `$` moderate (full-table scan + in-memory agg) ·
`$$` heavy (parse the per-game JSON series across **all** `PlayerGameStat`) ·
`$$$` very heavy (deserialize **all** `GameSnapshot`/`GameTurnEvents` blobs — must be precomputed by a job, never on page load).

**Data** — availability:
`✓` derivable now · `⊕` needs a **new tracking table / periodic snapshot** (we only store *latest* state, not history) ·
`⚠` empty until a gated feature lands (`A1` email send, `E1` messaging).

Rolling windows (`24h / 7d / 30d / 90d / all-time`) apply to any metric backed by a timestamp; `KPIΔ` implies the current window vs the previous one.

---

## 1. Users & Identity  *(source: `AppUser` / Identity, `AspNetUserRoles`)*

`AppUser` fields: `Id, UserName, Email, EmailConfirmed, PhoneNumber, TwoFactorEnabled, AccessFailedCount,
LockoutEnabled, LockoutEnd, IsEnabled, LastLoginUtc, LastActiveUtc, NumberOfWins/Losses/Draws, AvatarColour,
AvatarImageName`. Roles: `SystemAdmin, Admin, Restricted, HiddenUser, GithubManager`.

| Metric                                                             | Widget | Tier | Cost | Data |
|--------------------------------------------------------------------|---|---|---|---|
| *Total users                                                       | KPI | A | ¢ | ✓ |
| *Enabled vs disabled                                               | Pie / KPIΔ | A | ¢ | ✓ |
| *Email-confirmed rate                                              | Gauge | A | ¢ | ✓ |
| *2FA-enabled rate                                                  | Gauge | A | ¢ | ✓ |
| *Currently locked-out accounts                                     | KPI / Alert | A | ¢ | ✓ |
| *Accounts with `AccessFailedCount > 0` (brute-force watch)         | KPI / Alert | A | ¢ | ✓ |
| Role distribution (count per role)                                 | Bar | A | ¢ | ✓ |
| *Admins / SystemAdmins / GithubManagers count                      | KPI ×3 | A | ¢ | ✓ |
| *Restricted users (moderation lever in use)                        | KPI | A | ¢ | ✓ |
| Hidden users (`HiddenUser`)                                        | KPI | A | ¢ | ✓ |
| Users with a phone number                                          | Gauge | A | ¢ | ✓ |
| *Avatar customisation: % custom colour / % custom image            | Gauge ×2 | A | ¢ | ✓ |
| *Most-used avatar images                                           | Top-N | A | ¢ | ✓ |
| * **Active now** (LastActiveUtc < 5 min — PresenceService cadence) | KPI | A | ¢ | ✓ |
| DAU / WAU / MAU (active in 1d/7d/30d by LastActiveUtc)             | KPI ×3 | A | ¢ | ✓ |
| "Stickiness" DAU/MAU ratio                                         | Gauge | A | ¢ | ✓ |
| *Days-since-last-login histogram                                   | Hist | A | ¢ | ✓ |
| Days-since-last-active histogram (dormancy)                        | Hist | A | ¢ | ✓ |
| *Dormant accounts (no activity 30/60/90d)                          | KPI / Alert | A | ¢ | ✓ |
| Never-logged-in accounts (LastLoginUtc null)                       | KPI | A | ¢ | ✓ |
| *New registrations over time                                       | Line / KPIΔ | A | ¢ | ⊕¹ |
| Registration → first-login funnel                                  | Funnel | A | $ | ⊕¹ |
| Registration → first-game funnel                                   | Funnel | A | $ | ⊕¹ |
| **DAU/MAU history** (trend, not just today)                        | Line | A | ¢ | ⊕² |
| *Logins over time                                                  | Line | A | ¢ | ⊕² |
| *W/L/D totals across all users                                     | KPI ×3 | A | ¢ | ✓ |
| *Win-rate distribution across users                                | Hist | A | $ | ✓ |
| Top players by wins / win-rate / games                             | Top-N | A | $ | ✓ |
| *Cohort retention (registration-week → still-active-after-N-weeks) | Heat | A | $ | ⊕¹ |

¹ Needs a user **created** timestamp — verify JC.Identity `BaseUser` exposes one; if not, add a column or infer from the first audit entry.
² We store only *latest* `LastLoginUtc`/`LastActiveUtc`; a history line needs a small daily snapshot job (a `DailyActiveUsers` row).

**Graphs:** registrations Line; role Bar; activity-recency Hist; DAU/MAU dual-line; retention cohort Heat.

---

## 2. Games & Lifecycle  *(source: `Game`, `GamePlayer`, `GameTurn`)*

`Game`: `State{Setup,InPlay,Finished,Cancelled}`, `Outcome{None,Winner,Drawn}`, `RoundingRule{None,To5,To10,To20,To50}`,
`BoardId`, `CreatedUtc`, `LastModifiedUtc`, `JoinCode`. `GamePlayer`: `OrderId(0–7)`, `Dice1/2`, `PlayerGameOutcome{Winner,Drawn,Loser}`.

| Metric                                                                     | Widget | Tier | Cost | Data |
|----------------------------------------------------------------------------|---|---|---|---|
| *Total games (all-time)                                                    | KPI | S | ¢ | ✓ |
| *Games by state (Setup/InPlay/Finished/Cancelled)                          | Pie / Bar | S | ¢ | ✓ |
| *Live games right now (InPlay)                                             | KPI | S | ¢ | ✓ |
| *Games awaiting players (Setup)                                            | KPI | S | ¢ | ✓ |
| *Games created over time                                                   | Line / KPIΔ | S | ¢ | ✓ |
| *Games concluded over time (Finished + Cancelled)                          | Line | S | ¢ | ✓ |
| *Completion rate (Finished / created)                                      | Gauge | S | ¢ | ✓ |
| *Cancellation rate (Cancelled / created)                                   | Gauge | S | ¢ | ✓ |
| *Abandonment watch: InPlay with no turn in N weeks                         | KPI / Alert | S | $ | ✓ |
| *Outcome split: Winner vs Drawn (finished games)                           | Pie | S | ¢ | ✓ |
| *Players-per-game distribution (2…8)                                       | Hist | S | $ | ✓ |
| *Average players per game                                                  | KPI | S | $ | ✓ |
| Total player-participations (`GamePlayer` rows)                            | KPI | S | ¢ | ✓ |
| *Rounding-rule popularity                                                  | Bar | S | ¢ | ✓ |
| *Outcome / length by rounding rule                                         | Bar | S | $ | ✓ |
| *Board-skin usage (games per `BoardId`; default vs custom)                 | Bar / Top-N | S | ¢ | ✓ |
| *Avg turns per game                                                        | KPI | S | $ | ✓ |
| *Turn-count distribution (game length)                                     | Hist | S | $ | ✓ |
| *ongest / shortest games (by turns)                                        | Top-N | S | $ | ✓ |
| *Real-time game duration (last turn − first turn) distribution             | Hist | S | $ | ✓ |
| *Very short games (<5 turns — rage-quit/abandon signal)                    | KPI / Alert | S | $ | ✓ |
| *Turn-throughput: turns recorded per day                                   | Line | S | ¢ | ✓ |
| *Join-code collisions / reissues (ops sanity)                              | KPI | S | ¢ | ✓ |
| Peak concurrent live games (by hour-of-day / day-of-week)                  | Heat | S | $ | ⊕² |
| *Snapshot/event storage footprint (Σ `LEN(StateJson)` + `LEN(EventsJson)`) | KPI / Area | S | $ | ✓³ |
| *Storage reclaimable now (soft-deleted history past retention)             | KPI | S | $ | ✓ |

³ Project `LEN()` in SQL (never transfer blobs) — same trick `GetGameDetail` uses.

**Graphs:** created-vs-concluded dual-Line; state Pie; game-length Hist; players-per-game Hist; concurrency Heat (hour×weekday); storage-over-time Area.

---

## 3. Economy & Money  *(source: `PlayerGameStat` — the materialised per-player-per-game record)*

The richest seam: ~130 fields per player per finished game. All money figures are `uint`/`long` and aggregate
(Σ / avg / min / max) across players and games. **Cost `$$`** because meaningful economy KPIs scan the whole
`PlayerGameStat` table — strongly recommend a **precomputed daily metrics row** (see §15).

### 3.1 Money supply & circulation
| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Total money **minted** (Σ `MoneyEarned`, all player-games) | KPI | S | $$ |
| Total money **removed** (Σ `MoneySpent`) | KPI | S | $$ |
| Net circulation (minted − removed) | KPI | S | $$ |
| Income by source — Rent / GO / Cards / Free Parking / Selling / Mortgaging / Triples / Snake-eyes / Dice-number / Deals / Bankrupt-players | Bar / Pie | S | $$ |
| Spend by reason — Property / Building / Unmortgage / Turn-tax / Fines / Jail / Loan-repay / Rent / Deals | Bar / Pie | S | $$ |
| **Turn-tax collected** (Σ `SpentOnTurnTax`) — anti-snowball efficacy | KPI / Line | S | $$ |
| Total rent economy (Σ `RentEarned`) | KPI | S | $$ |
| Free-Parking throughput (Σ `MoneyFromFreeParking`) | KPI | S | $$ |
| Largest single payment ever (max `LargestSinglePayment` + reason badge) | Top-N | S | $$ |
| Largest rent payment ever (+ property) | Top-N | S | $$ |

### 3.2 Wealth outcomes & inequality
| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Final balance distribution (median / percentiles) | Hist | S | $$ |
| Final net-worth distribution | Hist | S | $$ |
| Peak net-worth distribution | Hist | S | $$ |
| Richest single game ever (max `FinalNetWorth`) | Top-N | S | $$ |
| Highest single-game balance | Top-N | S | $$ |
| **Gini coefficient** of final net worth (inequality index) | KPI / Gauge | S | $$ |
| Closest finishes (min stdev of finishers' net worth) | Top-N | S | $$ |
| Avg net cash-flow per game (and +/- split) | KPI / Hist | S | $$ |

### 3.3 Wealth over time (the JSON series — `$$`)
`BalanceOverTimeJson`, `NetWorthOverTimeJson`, `PropertyCountOverTimeJson`, `WealthRankOverTimeJson` are per-turn arrays.
| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Average balance **by turn number** (the "typical economy curve") | Line | S | $$ |
| Average net worth by turn | Line | S | $$ |
| Average property count by turn | Line | S | $$ |
| Wealth-rank volatility (do leaders flip? stdev of `WealthRankOverTime`) | KPI | S | $$ |
| Lead-change frequency / "comeback" rate (winner not leading at mid-game) | KPI | S | $$ |
| Early vs mid vs late-game wealth concentration | Bar | S | $$ |

**Graphs:** income/spend Pie pair; economy-curve Line (balance & net worth by turn); net-worth Hist; turn-tax-over-time Line; Gini Gauge.

---

## 4. Gameplay mechanics  *(source: `PlayerGameStat`)*

### 4.1 Dice & movement
Total/avg rolls, doubles, triples, snake-eyes frequency; your-dice-number events (you vs others rolled it);
direction changes; distance clockwise/counter/total; **board-landing heatmap** (`MostLandedOnBoardIndex` +
`…Count` → which spaces get hit most); GO/Free-Parking/Tax landing rates.
→ Widgets: KPIs, frequency Bars, **`Board` heatmap of landings**, doubles-rate Gauge. Tier `S`, cost `$$`.

### 4.2 Jail
Times sent to jail; exit-method split (paid / card / dice); avg jail turns; jail-economy spend.
→ Pie (exit method), KPIs, Hist (sentence length). Tier `S`, cost `$$`.

### 4.3 Cards
Cards taken vs played; utilisation rate (played/taken); hoarding (`CardsNeverPlayed`); instant-play share;
immunity taken/played; **per-`CardType` draw/play distribution** (12 types, from the JSON dicts — per-game only,
so aggregate by reparsing); most-fired `CardTrigger`; most-used `CardEngagement` (Forced/Choice/ResolveOnDraw);
total card payouts (Σ `MoneyFromCards`).
→ Bar (by card type), Gauge (utilisation), Top-N (most-fired trigger). Tier `S`, cost `$$`.

### 4.4 Property & development
Properties acquired / lost / purged; churn ratio; max complete sets (+ turn peaked); **set profitability ranking**
(avg rent by `PropertySet`); most/least profitable property (mode of board index); houses/hotels built (Σ
`SpentBuilding` ÷ unit cost); building spend by set.
→ Bar (set profitability), `Board` heatmap (profit by space), KPIs, Hist. Tier `S`, cost `$$`.

### 4.5 Loans & mortgages
Loans taken / repaid / outstanding; avg loan size; repayment rate; debt→bankruptcy correlation; mortgage
cycles; mortgage fees paid (GO).
→ KPIs, Gauge (repayment rate), Scatter (debt vs bankruptcy). Tier `S`, cost `$$`.

### 4.6 Deals (player trades)
Total money in deals (Σ `MoneyGivenInDeals` + `MoneyFromDeals`); deal balance (give vs receive); properties moved.
→ KPIs. Tier `S`, cost `$$`.

### 4.7 Endgame
Bankruptcy rate (% `Bankrupted`); voluntary-bankruptcy share; bankrupted-by-amount distribution; turns-survived
distribution; bankruptcy-timing (which turn players go bust).
→ Gauge, Hist (turns survived), Line (bankruptcy timing). Tier `S`, cost `$$`.

---

## 5. Player performance & leaderboards  *(source: `AppUser` W/L/D + `PlayerGameStat`)*

The public leaderboard ranks by:
`score = 1000 + wins·30 + draws·10 − losses·15 + winRate·200 − max(0, 10−games)·20`
(tiebreak: wins → win-rate → fewer losses). The admin view can show the **unfiltered, non-anonymised** board.

| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Top players by leaderboard score | Top-N | A | $ |
| Top by win-rate (min-games gated) | Top-N | A | $ |
| Top by games played (engagement) | Top-N | A | $ |
| Skill distribution (score percentiles) | Hist | A | $ |
| Win-rate tiers (0–20…80–100%) | Bar | A | $ |
| Games-played tiers (1–5, 5–10, 10–20, 20+) | Bar | A | ¢ |
| **Hall of fame** (single-game records): richest game, longest game, most-bankruptcies game, highest balance, most rent | Top-N panel | S | $$ |
| **Player milestones**: most games, most total wins, most properties acquired, most rent earned, most cards played, longest jail time, most deals | Top-N panel | S | $$ |
| Players with 0% win-rate at high game count (struggle signal) | Table | A | $ |

---

## 6. Social graph  *(source: `Friend`, `FriendRequest`, `BlockedUser`)*

| Metric | Widget | Tier | Cost | Data |
|---|---|---|---|---|
| Total friendships (active) | KPI | A | ¢ | ✓ |
| Friendships formed over time | Line / KPIΔ | A | ¢ | ✓ |
| Friendships removed (`DateRemovedUtc`) over time | Line | A | ¢ | ✓ |
| Friends-per-user distribution | Hist | A | $ | ✓ |
| Most-connected users | Top-N | A | $ | ✓ |
| Avg friends per user | KPI | A | $ | ✓ |
| Users with zero friends (isolation) | KPI | A | $ | ✓ |
| Friend-requests sent over time | Line | A | ¢ | ✓ |
| Pending requests (null `IsAccepted`) | KPI | A | ¢ | ✓ |
| Acceptance rate (accepted / acted-on) | Gauge | A | ¢ | ✓ |
| Decline rate | Gauge | A | ¢ | ✓ |
| Avg time-to-respond (`AcknowledgedAtUtc − CreatedUtc`) | KPI / Hist | A | $ | ✓ |
| Stale pending requests (age > N days) | KPI | A | ¢ | ✓ |
| Request funnel: sent → accepted → friendship | Funnel | A | $ | ✓ |
| Total active blocks | KPI | A | ¢ | ✓ |
| Blocks created over time | Line | A | ¢ | ✓ |
| Most-blocked users (abuse signal) | Top-N / Alert | A | $ | ✓ |
| Users who have blocked someone (% of base) | Gauge | A | $ | ✓ |
| Reciprocal blocks | KPI | A | $ | ✓ |

**Graphs:** friendship growth/removal dual-Line; friends-per-user Hist; request Funnel; block-rate Line with spike Alerts.

---

## 7. Moderation  *(source: `ReportedUser`, `BlockedWord`, + report actions in `AdminActionLog`)*

`ReportReason{Harassment,HateSpeech,Threats,InappropriateContent,SelfHarm,Spam,Impersonation,Other}`;
`ReportResolution[Flags]{Open,Handled,AccountRestricted,AccountDisabled,AccountDeleted}`.

| Metric | Widget | Tier | Cost |
|---|---|---|---|
| **Open reports** (queue depth) | KPI / Alert | A | ¢ |
| Reports over time | Line / KPIΔ | A | ¢ |
| Reports by reason | Bar / Pie | A | ¢ |
| Reports by resolution state | Bar | A | ¢ |
| Resolution rate (handled / total) | Gauge | A | ¢ |
| Avg time-to-resolve (resolution `LastModifiedUtc − CreatedUtc`) | KPI | A | $ |
| Oldest unresolved report (SLA breach) | KPI / Alert | A | ¢ |
| Most-reported users (repeat offenders) | Top-N | A | $ |
| Outcome mix on reported users (restricted/disabled/deleted) | Bar | A | ¢ |
| Reason → action correlation (which reasons lead to bans) | Heat | A | $ |
| Reporter activity / possible false-reporters | Top-N | A | $ |
| Self-harm/threats flagged (priority queue) | KPI / Alert | A | ¢ |
| Blocked-word list size | KPI | A | ¢ |
| Blocked words added over time / by admin | Line / Bar | A | ¢ |
| Profanity-filter hit rate (rejections) | Line | A | $ | ⊕³ |

³ The filter currently logs matched term/source to the audit/log but there's no rejection counter — needs a small tally to trend "names blocked."

---

## 8. Content — Boards & Cards  *(source: `BoardSkin`, `BoardSkinSpace`, `SharedBoardSkin`, `PersistedCardIds`)*

| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Total custom boards | KPI | A | ¢ |
| Boards created over time | Line | A | ¢ |
| Boards per creator (top creators) | Top-N | A | $ |
| Avg spaces per board | KPI | A | $ |
| Space-type distribution across boards (`BoardSpaceType`) | Bar | A | $ |
| Property-set coverage / balance | Bar | A | $ |
| Shared boards (count, % of boards shared) | KPI / Gauge | A | ¢ |
| Most-shared boards | Top-N | A | $ |
| Share reach (avg recipients per shared board) | KPI | A | $ |
| Boards actually used in games (join to `Game.BoardId`) | KPI / Gauge | S | $ |
| Orphaned/unused boards | KPI | A | $ |
| Total cards in catalogue (`PersistedCardIds`) | KPI | A | ¢ |
| Cards added over time | Line | A | ¢ |
| Action-count / condition-count distribution per card | Hist | A | $ |
| Most-complex cards (action/group count) | Top-N | A | $ |
| Card usage (drawn/played in games — join card stats) | Top-N | S | $$ |
| Never-used cards (dead content) | Table | S | $$ |

---

## 9. Communications  *(source: `EmailLog*`, `Notification`/`NotificationLog`, `ThreadActivityLog`)*

### 9.1 Email *(⚠ empty until A1 — sender provisioned)*
| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Emails sent over time | Line / KPIΔ | A | ¢ |
| **Delivery success rate** (`EmailSentLog.Succeeded`) | Gauge / Alert | A | ¢ |
| Failures over time | Line / Alert | A | ¢ |
| Success rate by `EmailProvider` (Console/Microsoft/SmtpRelay/DirectSmtp) | Bar | A | ¢ |
| Recipient-type split (To/Cc/Bcc) | Pie | A | ¢ |
| Retry rate (multiple `EmailSentLog` per `EmailLog`) | KPI | A | $ |
| Top error messages | Table | A | $ |
| Top subjects / senders | Top-N | A | ¢ |

### 9.2 Notifications
| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Notifications created over time | Line | A | ¢ |
| By `NotificationType` (Message/Info/Success/Warning/Error/System/Task) | Bar | A | ¢ |
| **Read rate** (IsRead) | Gauge | A | ¢ |
| Avg time-to-read (`ReadAtUtc − CreatedUtc`) | KPI / Hist | A | $ |
| Dismissed (soft-deleted) rate | Gauge | A | ¢ |
| Expired-before-read rate | Gauge | A | $ |
| Users with high unread backlog | Top-N | A | $ |
| GithubManager issue-notification volume (the existing notify-on-report flow) | KPI | A | ¢ |

### 9.3 Messaging *(⚠ empty until E1)*
`ThreadActivityType{Message,ParticipantAdded,ParticipantRemoved}`.
| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Messages sent over time | Line | A | ¢ |
| Activity-type split | Pie | A | ¢ |
| Most-active threads | Top-N | A | $ |
| Participant add/remove churn | Line | A | ¢ |
| Active vs dormant threads | KPI | A | $ |

---

## 10. Issues / Feedback  *(source: `ReportedIssue`, `IssueComment` — JC.Github)*  `Tier G/A/S`

| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Open vs closed issues | Pie / KPI | G | ¢ |
| Bugs vs suggestions (`IssueType`) | Bar | G | ¢ |
| Issues reported over time | Line / KPIΔ | G | ¢ |
| **GitHub sync failures** (`ReportSent == false`) | KPI / Alert | G | ¢ |
| Resolution rate (closed / total) | Gauge | G | ¢ |
| Avg time-to-close (`Closed` − `Created`) | KPI | G | $ |
| Issues with screenshots (`Image`) | KPI | G | ¢ |
| Issues with client metadata captured | KPI | G | ¢ |
| Top reporters (local `UserId`) | Top-N | A | $ |
| Comment activity (total, per issue) | KPI / Top-N | G | ¢ |
| Issues with active discussion vs silent | Bar | G | $ |
| GitHub-origin vs in-app-origin issues | Pie | G | ¢ |
| **Reporters contacted** (the new IssueContact flow — `IssueReporterContacted` logs) | KPI / Feed | A | ¢ |

---

## 11. Admin activity & accountability  *(source: `AdminActionLog`)*

`AdminActionType` (19): RoleAdded/Removed, UserDisabled/Enabled/Deleted, UserDisplayNameUpdated, UserHidden/Shown,
ReportResolved, GameDrawn/Cancelled/Deleted/Refresh/Reverted, RulesUpdated, TurnTaxUpdated, SettingsUpdated,
StatisticsRecomputed, IssueReporterContacted. `AdminTargetType`: User/Game/Report/Role/Config/Issue.

| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Admin actions over time | Line / KPIΔ | A | ¢ |
| By action type | Bar | A | ¢ |
| By target type | Pie | A | ¢ |
| **Per-admin leaderboard** (who's doing the work) | Top-N | A | ¢ |
| Recent admin actions | Feed | A | ¢ |
| **Your** recent actions | Feed | Self | ¢ |
| Destructive actions (deletes/reverts) count + feed | KPI / Feed | A | ¢ |
| Config-change history (rules/turn-tax/settings) | Feed | S | ¢ |
| Moderation funnel: reports resolved → users restricted → disabled → deleted | Funnel | A | $ |
| Action mix per admin (creates vs destructive) | Bar | A | $ |

---

## 12. Audit trail  *(source: JC.Core `AuditEntry`)*

`AuditAction{Create,Update,SoftDelete,Delete,Restore}`; fields `TableName, EntityKey, UserId/UserName, AuditDate, ActionData`.
System actor = `System__ID`, unauthenticated = `Unknown__ID`.

| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Audit entries over time (DB write velocity) | Line | A | ¢ |
| By action type | Bar | A | ¢ |
| **By table** (which tables churn most) | Bar / Top-N | A | ¢ |
| System vs human vs unknown actor split | Pie | A | ¢ |
| Most-active actors (audited mutations per user) | Top-N | A | ¢ |
| Soft-delete vs restore balance (per table) | Bar | A | ¢ |
| Audit-table size / age (retention planning) | KPI / Hist | A | ¢ |
| Bulk-change spikes (anomaly — many same-actor same-second entries) | Alert | A | $ |

---

## 13. System / Ops / Background jobs  *(source: Hangfire monitoring API, `GameSettings`, retention queries)*

Recurring jobs: `PresenceFlushJob` (*/5m), `StatisticsJob` (01:00 & 13:00), `GameCleanupJob` (03:00),
`CancelledGameCleanupJob` (03:30), `GameAbandonmentJob` (04:00), `SnapshotCleanupJob` (04:30).

| Metric | Widget | Tier | Cost |
|---|---|---|---|
| Hangfire: succeeded / failed / processing / scheduled counts | KPI ×4 | S | ¢ |
| **Failed jobs** (last 24h) | KPI / Alert | S | ¢ |
| Recurring-job roster + last-run / next-run / last-status | Table | S | ¢ |
| Job success-rate trend | Line | S | ¢ |
| Per-job last duration | Bar | S | ¢ |
| Quick link to `/hangfire` | Button | S | — |
| **Stats backlog**: finished games missing `PlayerGameStat` | KPI / Alert | S | $ |
| Retention preview: records soft-deleted past each window (purge-eligible now) | KPI ×N | S | $ |
| Storage footprint trend (snapshots/events bytes) | Area | S | $ | ⊕² |
| Abandoned-game candidates (next abandonment sweep) | KPI | S | $ |
| Effective `GameSettings` snapshot (retention/abandon config at a glance) | Table | S | ¢ |
| Presence-flush lag (in-memory vs DB last-active delta) | KPI | S | $ |
| Profanity word-list size + last update | KPI | A | ¢ |

---

## 14. Cross-domain, cohort, anomaly & "moonshot"

Higher-effort, higher-insight ideas that join domains or mine the raw blobs.

**Correlation / behavioural**
- Win-rate vs games-played `Scatter` (does experience help?).
- Bankruptcy vs outstanding-loans `Scatter`.
- Friend-count vs games-played `Scatter` (social → engaged?).
- Aggression index: deals + blocks + reports per user.
- Report → subsequent-restriction conversion (moderation efficacy).

**Cohorts & retention `⊕`**
- Registration-week cohorts → games played / still-active over time `Heat`.
- First-game outcome (win/lose/bankrupt) → retention (does a bad first game churn users?).
- New-user → first-friend → first-game activation funnel.

**Anomaly / fairness `Alert`**
- Money-minting outliers (>3σ economy).
- Players with >50% bankruptcy rate.
- Sub-5-turn games cluster (abuse/abandon).
- Unusual dice-luck (doubles/triples far from expected) — possible mis-entry.
- Same-IP / same-time mass registrations (needs request metadata).

**Moonshot — raw snapshot/event mining `$$$` (precompute only)**
- True **board-landing heatmap** from every turn's position (beyond the per-game most-landed field).
- Property ownership/monopoly frequency by space across all games.
- Auction analytics (bids, final prices vs face value) from `GameTurnEvents`.
- Rent-paid-by-space heatmap (which squares hurt most).
- Average game economy "movie" (net worth of all players animated over turns).
- Card-effect impact (avg net-worth swing after each card type).
- Turn-duration analytics (real seconds per turn from event timestamps) → pace/bottleneck.

---

## 15. Implementation notes (for whatever gets picked)

- **Precompute the heavy stuff.** Anything `$$`/`$$$` (economy, JSON-series, snapshot mining) must **not** run on
  page load. Add a `DashboardMetricsJob` (Hangfire, e.g. hourly/daily) that writes a single `DashboardSnapshot`
  row (JSON of computed KPIs/series); the page reads that. Cheap `¢` counters can run live.
- **One `DashboardService`** composing the cheap live queries + the latest `DashboardSnapshot`, mirroring the
  area's service pattern; ctor-gated `Admin`/`SystemAdmin`. Tier-filter sections by the viewer's role.
- **History needs capture.** `⊕` metrics (DAU/MAU history, registration trend, storage-over-time) require a tiny
  periodic snapshot table — we only persist *latest* state today. Decide which are worth a tracking row.
- **Tier the layout.** SystemAdmin sees game/economy/ops; Admin sees users/social/moderation/comms/issues;
  GithubManager (if ever shown a dashboard) sees only Issues. Hide empty `⚠` sections until A1/E1.
- **Charts.** No chart lib is in the project yet (the stat pages render bespoke). A dashboard with `Line`/`Bar`/
  `Pie`/`Heat` wants a small client lib (Chart.js is the lightweight default) or server-rendered SVG sparklines.
- **Windows.** Bake a shared window selector (24h/7d/30d/90d/all) so timestamped widgets share one control.

---

## 16. Decisions to make (for curation)

1. **Game/economy tier** — game data is SystemAdmin-gated today (`AuthCheck()`). Do read-only *aggregate* game/
   economy widgets stay SystemAdmin-only, or do we expose them to Admins (read-only ≠ management)?
2. **Live vs precomputed** — confirm the `DashboardMetricsJob` + snapshot-row approach for heavy metrics.
3. **Which `⊕` trackers** to add (DAU/MAU history, registration timestamp, storage-over-time, profanity-hit tally).
4. **Chart library** — Chart.js vs server-SVG vs counters-only first cut.
5. **Scope of v1 dashboard** — pick the headline KPI strip + 2–4 charts + the ops/moderation alert tiles; the
   rest of this catalogue is the backlog.

---

## 17. Traceability
- `c1-admin-area.md` — the area this dashboard fronts (auth matrix §4, the services that own each query).
- `game-stats.md` — the `PlayerStatRecord` projection (§3/§4 economy + gameplay metrics derive from it).
- `StatCatalogue.cs` / `StatRender.cs` — the existing per-player stat presentation (reuse formatting helpers).
- `LeaderboardService` — the ranking formula (§5).
- `ServiceRegistration.cs` — the Hangfire job roster (§13).