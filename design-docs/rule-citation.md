# Rule Citations — In-Game Rule Explainability

How the engine narrates *which rules fired* during a turn, so the table can
answer "w, why did that happen, and was that even in the rules?". The host
page surfaces a **"Rules Occurred this Turn"** panel listing the rules that
governed the turn, each backed by its `game-rules.md` text. The same rule
catalogue also drives a full, browsable rules page.

This is the *rule-provenance* counterpart to `event-receipts.md`: receipts record
**what changed**; citations record **which rule explains it**. The two are sibling
streams, deliberately kept apart (§3).

**Status:** design, pre-implementation. The pieces it mirrors (`EventReceipt` /
`IEventEmitter` / `GameCacheModel.AddEvent` / `ClearEvents`, the `StateChanged`
broadcast) already exist. Nothing in this doc is built.

---

## 1. Purpose & Scope

The ruleset diverges sharply from standard Monopoly (three dice, reserved
properties, loans, Free Parking takes, percentage cards, …), so players around
the table routinely hit "wait — *why* did that just happen?". The app already
knows: the engine applied a specific rule. Rule citations surface that rule.

This is the **helper, not simulator** principle (`game-engine.md` §1) applied to
explainability. The app does **not** narrate precisely what happened to whom —
the players can see the board and are playing the game. It supplies the **rule
text as context**; the humans trivially decipher the *who* (whoever's on Free
Parking) and the *why* (they hold no double hotel, so the cap is £1000). Citing
the rule is enough; simulating the narration is not the job.

**In scope:**
- A per-turn stream of rule citations emitted by the engine as rules fire.
- Surfacing that stream live on the host "Rules Occurred this Turn" panel.
- A structured rule catalogue (`rules.json`) that resolves a citation to its
  player-facing text — and doubles as the source for a full rules page.

**Out of scope:**
- Per-player / per-amount narration or attribution (deliberately — §7).
- Persisting citations as history (they are ephemeral, live-only — §9).
- Being a source of truth for *behaviour* — the engine code and `game-rules.md`
  remain that (§10). Citations are a derived, presentational trail.

---

## 2. The Core Decisions

The non-negotiables the rest of the design follows from.

1. **A sibling stream to event receipts**, not a field on them — rules and
   receipts are not 1:1 (§3).
2. **The engine emits codes; the web resolves text.** The engine stays pure
   (no JSON, no presentation); a web-side `RuleCatalog` maps code → text (§4).
3. **Codes are stable semantic keys.** The hierarchical rule *number*
   (`3.2.a`) is display metadata, never the identity (§5).
4. **Granularity follows the engine's branches**, not the document's
   typography. A node earns a code when the engine distinctly applies it (§6).
5. **Static rule text only — no templating.** A citation carries a code and
   nothing else; the rule text is fixed prose (§7).
6. **Drift is guarded by a lockstep test**, not source generation — a
   hand-written enum, with a test asserting every code has a catalogue entry
   (§10).

---

## 3. A Sibling Stream to Event Receipts

Citations mirror the receipt plumbing beat-for-beat, but are a **separate
stream** — because a rule and a state change do not correspond 1:1:

- A single rule (Double 3) produces *two* `PlayerMovedReceipt`s.
- The reservation mechanic produces a `PropertyTransferReceipt(Reserved)` but is
  itself a higher-level rule with no dedicated receipt.
- The £1000 Free Parking cap *shaped* the amount on a `FinancialTransactionReceipt`
  but is not itself a state change.

So a `RuleCode` is **not** added to `EventReceipt`; `RuleCitation` is its own
stream with its own seam.

| | `EventReceipt` (built) | `RuleCitation` (new) |
|---|---|---|
| Question answered | what changed | which rule explains it |
| Seam | `IEventEmitter.Emit` | `IRuleEmitter.Cite` |
| Cache store | `AddEvent` / `Events` | `AddRuleCitation` / `RuleCitations` |
| Cleared | turn boundary (`ClearEvents`) | turn boundary (`ClearRuleCitations`) |
| Subject | player-scoped (`PlayerId`) | **table-wide** (no subject) |
| Payload | rich primitives (amounts, ids) | **just the code** |
| In the live `StateChanged` frame | **no** (`[JsonIgnore]`) | **yes** |
| Purpose | stats / history | live explainability |

The last two rows are the important contrast and they *sharpen* both streams:
receipts are deliberately excluded from the live frame because the live UI
renders from cache state, not the receipt stream (`web-orchestration.md` §6 /
the live-view-cache decision). Citations are the **inverse** — they exist
*for* the live UI, are tiny (bare codes), and so ship **in** the `StateChanged`
frame. Each stream's identity is now crisp: **receipts = history/stats (out of
frame), citations = live explainability (in frame).**

And citations carry **no subject** — a rule is a table-wide fact ("Free Parking
takes are capped at £1000"), not something "about" one player. This is another
reason they are leaner than receipts, and it matches §7's no-narration stance:
the citation states the rule; the table knows whom it applied to.

---

## 4. Engine Emits Codes, Web Resolves Text

The engine is a pure library — no IO, no presentation (`game-engine.md` §2/§3).
It must not load a rules file or hold rule prose. So the split is:

- **Engine** owns a `RuleCode` enum (the shared contract) and emits citations as
  **codes only**. It never sees rule text. Stays pure and unit-testable.
- **Web** owns `RuleCatalog` — loads `rules.json` (keyed by `RuleCode`),
  resolves `code → RuleEntry { title, text, display coordinates, doc anchor }`,
  and serves **both** consumers: the host "Rules This Turn" panel (resolve the
  turn's cited codes) and the full rules page (render every entry).

This mirrors the existing pattern exactly: the engine defines `FinancialReason`;
the web renders it. Same here — the engine defines `RuleCode`, the web renders
the prose. The dual use (in-game panel + rules page) falls out of the one
catalogue with no extra mechanism.

---

## 5. The Identifier Scheme — Semantic Keys, Numbers as Display Metadata

Rules are organised hierarchically — **section → rule → point** (e.g. *3.
Movement → 3.2 third-die movement → 3.2.a triple exception*). It is tempting to
make that path the identifier (`"03_02_A"`). **Do not.** A positional code is
brittle: insert or reorder a rule — which the ruleset's own history shows happens
(Loans rewritten, Auctions inserted wholesale, Reserved-Properties reworded) —
and every downstream code silently shifts. Old `3.2` becomes `3.3`; every
`Cite(3.2)` now points at the wrong rule, with no compiler help. This is the
**keys-not-indexes** lesson the project already adopted for `CardOptionPrompt`
(`choice-events.md` §15.9): stable keys "so a later revision that reorders
options doesn't silently change the meaning".

So:

> **The identity is a stable semantic key** (the `RuleCode` enum member). **The
> hierarchical number is display metadata** living in the catalogue, never the
> engine's reference.

A catalogue entry, keyed by code:

```jsonc
"Move_ThirdDie": {
  "section": 3, "rule": 2, "point": null,        // display coordinates → "3.2"
  "title": "Third die moves the other players",
  "text": "After the rolling player has moved, the third die dictates how far every other player moves, in clockwise turn order.",
  "doc": "game-rules.md#movement"
},
"Move_ThirdDie_TripleException": {
  "section": 3, "rule": 2, "point": "a",          // → "3.2.a"
  "title": "Triples skip third-die movement",
  "text": "On a triple the rolling player moves the combined total of all three dice and no other player moves.",
  "doc": "game-rules.md#triple-dice-rolls"
}
```

- The catalogue is a **flat dictionary** keyed by code — a direct lookup resolves
  a citation; the rules page **sorts by `(section, rule, point)`** to build the
  collapsible tree. (Flat-with-coordinates beats a nested tree: resolution is
  O(1) and the page derives nesting from the coordinates.)
- Reordering the document only edits the `section`/`rule`/`point` fields —
  citations never break.
- The catalogue **owns the display numbering**; `game-rules.md` stays prose
  (its `##` headings are unnumbered) and is reached via the `doc` anchor. Less
  coupling than forcing the two numberings to track each other.

---

## 6. Granularity — Citable Nodes Follow Engine Branches

The question "when is something a rule vs a sub-rule?" is the wrong axis. The
hierarchy is for *exposition*; citability follows the **engine's branches**:

> A node earns its own citable `RuleCode` when the engine has a **distinct
> branch that that statement governs**. Nesting depth is irrelevant — a
> sub-point and its parent are **siblings in citability**, parent/child only in
> *display*.

And the rule for *which* code to cite at runtime:

> **Cite the node whose statement actually governed the outcome.**

Worked through the third-die example:
- Normal or double roll → other players move on the third die → cite
  **`Move_ThirdDie`**.
- Triple → other players do **not** move → cite **`Move_ThirdDie_TripleException`**
  **and not** `Move_ThirdDie`, because the general statement did not govern —
  the exception overrode it.

Consequences:
1. **The catalogue is a superset of the citable set.** `rules.json` holds *every*
   rule node so the rules page can show the whole rulebook; the `RuleCode` enum
   is *only* the subset the engine cites. A non-citable node simply never appears
   in a "Rules This Turn" list but does appear on the rules page.
2. **Each citable node's `text` must read standalone.** It lands alone in the
   turn's list, so it cannot lean on its parent's sentence for meaning. Author
   citable text to stand on its own (the exception text above does).

---

## 7. Static Text Only — No Templating

A citation surfaces **fixed rule prose**, never a per-turn narration. It carries
**a code and nothing else** — no amounts, no player ids, no property names, no
context payload.

Rationale — the helper-not-simulator principle again: the players are at the
table watching. Showing *"You are capped at £1000 for Free Parking, £2000 with at
least one double hotel"* gives the **context**; the table decodes the *who*
(whoever's on Free Parking) and the *why* (no double hotel) instantly.
Templating the citation into *"Bob took £1000 because he has no double hotel"*
would drag the engine toward simulating the narration — bigger, and error-prone
in exactly the way the ruleset's complexity makes dangerous.

What pure-static-text buys:
- **`RuleCitation` collapses to (almost) nothing** — just the code (§8).
- **`Cite(code)` calls pass zero runtime state** — pure markers; they cannot be
  "wrong", only present or absent.
- **Dedupe is trivial and correct.** With no per-instance payload, two
  `FreeParking_TakeCap` citations *are* identical, so "Rules This Turn" is the
  **distinct set of codes, ordered by first occurrence** — which reads as the
  turn's narrative for free (§12).
- **The only failure mode is a *missing* rule**, never a wrongly-contextualised
  one (§11).

---

## 8. Types

The seam mirrors `IEventEmitter`; the data is leaner (no subject, no payload).

```csharp
// Engine — the shared contract. Flat, semantic names, only the citable subset.
// Grouped by section with region comments; the number lives in the catalogue.
public enum RuleCode
{
    // ── Section 3 — Movement ──
    Move_DirectionOfTravel,
    Move_ThirdDie,
    Move_ThirdDie_TripleException,

    // ── Section 9 — Free Parking ──
    FreeParking_PayFee,
    FreeParking_TakeCap,
    FreeParking_HandInEligibility,
    FreeParking_TakeProperties,
    // … added as Cite points are written …
}
```

```csharp
// Engine seam — mirrors IEventEmitter.
public interface IRuleEmitter
{
    void Cite(RuleCode code);
}

internal sealed class RuleEmitter(GameCacheModel cache) : IRuleEmitter
{
    public void Cite(RuleCode code) => cache.AddRuleCitation(code);
}
```

The cache stores the **distinct, first-occurrence-ordered** set directly — no
wrapper type is needed because there is no payload (§7). Dedupe happens
**on add**:

```csharp
// GameCacheModel — mirrors AddEvent / Events / ClearEvents.
private readonly List<RuleCode> _ruleCitations = [];

// NOT [JsonIgnore]d — ships in the StateChanged frame (§3). Tiny: bare codes.
public IReadOnlyList<RuleCode> RuleCitations => _ruleCitations;

public void AddRuleCitation(RuleCode code)
{
    if (_ruleCitations.Contains(code)) return;   // dedupe on add → distinct set
    _ruleCitations.Add(code);                     // list order == first-occurrence order
    StampConcurrency();                           // same as AddEvent
}

public void ClearRuleCitations()
{
    _ruleCitations.Clear();
    StampConcurrency();
}
```

The `GameEngine` bundle exposes the emitter alongside the others:

```csharp
public IRuleEmitter RuleEmitter { get; } = new RuleEmitter(cache);
```

Web-side catalogue (not in the engine):

```csharp
public sealed record RuleEntry(
    RuleCode Code, int Section, int Rule, string? Point,
    string Title, string Text, string? Doc);

public interface IRuleCatalog
{
    RuleEntry Get(RuleCode code);                 // resolve one citation
    IReadOnlyList<RuleEntry> All();               // the full rules page, sorted by coordinates
}
```

---

## 9. Lifecycle

| Stage | Where | When |
|---|---|---|
| Citation | `IRuleEmitter.Cite(code)` → `cache.AddRuleCitation` | When a rule governs an outcome, as it happens |
| Live broadcast | inside the `StateChanged` cache frame | On each prompt-open and work-completion push (§3) |
| Per-turn clearing | `cache.ClearRuleCitations()` from `TransitionToNextPlayer` / `TransitionToExtraTurn` | Turn boundary, alongside `ClearEvents` |
| Restart | lost (cache is in-memory only) | Server restart, mid-turn |

1. **Per-turn scope.** Citations live for exactly one turn; the next turn starts
   empty — matching the receipt and prompt lifecycles.
2. **Ephemeral, not persisted.** Citations are a *live* feature, not history, so
   they are not written to the snapshot or any side table. (If a "rules on turn
   N" replay is ever wanted, they could ride the `GameTurnEvents` blob —
   `game-stats.md` — but that is explicitly not now.)
3. **Lost on restart**, like receipts and the pending prompt
   (`choice-events.md` §1) — the in-progress turn is re-rolled from the last
   snapshot, and its citations go with it. No integrity concern: the snapshot is
   canonical and carries no citations.

---

## 10. Drift Control — Hand Enum + Lockstep Test

Rules now live in three places: `game-rules.md` (prose contract), `rules.json`
(structured catalogue), and the engine (behaviour). Keep them honest:

1. **`game-rules.md` stays the authoritative deep contract.** `rules.json` is the
   **structured catalogue** keyed by `RuleCode`, holding concise player-facing
   text + display coordinates + a `doc` anchor back to the prose. It is the
   *presentation index* of the rules, not a competing source of behaviour — the
   same role `choice-events.md` §15 plays for prompt types.
2. **The `RuleCode` enum is hand-written**, grown as `Cite` points are added.
3. **A lockstep unit test** asserts the two agree: every `RuleCode` has a
   `rules.json` entry, and (optionally) every entry flagged citable has a
   matching code. Drift fails CI in either direction.

Source-generating the enum from `rules.json` (single source of truth, no
hand-typing) was evaluated and **dropped**: at the expected scale (dozens of
codes, authored alongside the citations) the lockstep test buys the same
drift-safety for a fraction of the machinery, and keeps the enum a plain,
greppable, debuggable file. The option remains a clean future upgrade (a
checked-in generated `RuleCode.g.cs` with a CI freshness check) if the code set
ever balloons — the citation plumbing and every call site are unchanged either
way.

---

## 11. Completeness — Only as Complete as the Cite Calls

Every line in "Rules This Turn" is downstream of a `Cite` call, so a rule that
fires without a citation is invisible — exactly as a stat is only as complete as
its receipts (`game-stats.md` §10). Two consequences:

1. **Cite as you build.** Adding a rule branch to a service means adding its
   `Cite(RuleCode.X)` (and its `rules.json` entry) in the same change — a
   standing review check, the citation analogue of the receipt producer
   convention (`event-receipts.md` §8).
2. **It fills in over time, and never lies.** Thanks to the static-text decision
   (§7), the worst an incomplete citation set can do is *omit* a rule — it can
   never surface a misleading one. Coverage grows; correctness is never at risk.

---

## 12. Worked Example

The scenario: a player rolls a **double 3**, moving forward 3 then back 3, and on
the forward leg lands on the final property of a set they otherwise own — so
(reserve rule active) they **reserve** it. Their **third die is a 5**, moving
another player onto **Free Parking**, where (no double hotel) they take **£1000**
and **hand in a pink** property.

The turn's distinct citations, in first-occurrence order:

| Rule (display) | `RuleCode` | Cited text (from `rules.json`) |
|---|---|---|
| 5.3 | `Double_ThreeMovement` | "On a double 3 the rolling player moves forward 3 spaces and acts, then back 3 spaces and acts." |
| 12.2 | `Reserved_ReserveFinalProperty` | "While the reserve rule is active, landing on the last property you need to complete a set lets you reserve it for 50% of its price, rather than buy it." |
| 3.2 | `Move_ThirdDie` | "After the rolling player moves, the third die moves every other player, in clockwise order." |
| 9.2 | `FreeParking_TakeCap` | "Taking from Free Parking is capped at £1000 — £2000 if you own a double hotel." |
| 9.3 | `FreeParking_HandInEligibility` | "You hand in one property from a set you have not built on and not previously handed a property in from." |
| 9.2 | `FreeParking_TakeProperties` | "You also take any properties currently sitting in Free Parking." |

Note how the list spans **two different players** (the roller's double + reserve;
another player's Free Parking resolution) yet carries **no subject** (§3): the
panel states the rules; the table knows the roller reserved and the other player
swept Free Parking. That is the helper-not-simulator line in action — context,
not narration.

---

## 13. Display Surfaces

Both consume the one `RuleCatalog`:

1. **"Rules Occurred this Turn" panel (host page).** A button on the game view
   opens a panel that reads `cache.RuleCitations` (already in the live frame),
   resolves each code via `RuleCatalog.Get`, and renders the static text lines —
   distinct, ordered by first occurrence. Refreshes on each `StateChanged` frame;
   empties at the turn boundary.
2. **Full rules page (web).** Renders `RuleCatalog.All()` grouped into the
   section → rule → point tree (sorted by the display coordinates), collapsible —
   a browsable rulebook generated from the same JSON, no separate content.

How exactly the panel is laid out (modal, drawer, side panel) is a UI decision,
not framework. The contract is: the codes are in the live frame, the catalogue
resolves them, the text is static.

---

## 14. Open / TODO

1. **Populate `rules.json`.** Author the catalogue from `game-rules.md` — every
   rule node for the page, with `section`/`rule`/`point`/`title`/`text`/`doc`.
   Decide whether to number `game-rules.md`'s sections to match, or let the JSON
   own numbering (this doc leans to the latter — §5).
2. **Seed the `RuleCode` enum + the `Cite` points** for the already-built paths
   (movement, doubles/triples, GO, rent, tax, jail, loans, mortgage fee,
   reservation, auction, Free Parking). Cards fill in with the card subsystem.
3. **The lockstep test** (§10) lands with the first codes.
4. **Panel UI + rules page** — the two display surfaces (§13).
5. **Persisted "rules on turn N"?** — deferred; revisit only if turn replay
   wants it (§9.2), riding the `game-stats.md` event blob.

---

## 15. Traceability

1. **`game-rules.md`** — the authoritative rule prose each citation points at
   (via the catalogue's `doc` anchor); the behavioural contract.
2. **`event-receipts.md`** — the sibling stream this mirrors; §3 here draws the
   receipts-vs-citations contrast (history/out-of-frame vs live/in-frame).
3. **`web-orchestration.md` §6** — the `StateChanged` whole-cache broadcast the
   citations ride in.
4. **`choice-events.md` §15.9** — the keys-not-indexes precedent the identifier
   scheme follows (§5).
5. **`game-engine.md` §1–§3** — helper-not-simulator (the no-narration stance,
   §7) and the pure-engine layering (engine emits codes, web resolves text, §4).
6. **`game-stats.md` §10** — the "only as complete as the receipts" discipline
   the completeness caveat (§11) parallels.
7. **Code (when built)** — `MP.GameEngine/Enums/RuleCode.cs`,
   `MP.GameEngine/Abstractions/IRuleEmitter.cs`,
   `MP.GameEngine/Services/Framework/RuleEmitter.cs`, the `GameCacheModel`
   citation hooks, and web-side `Services/.../RuleCatalog.cs` + `rules.json`.