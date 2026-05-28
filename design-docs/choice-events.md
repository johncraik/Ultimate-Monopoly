# Engine Prompts — Pause & Resume Framework

How the engine pauses, asks a player something, and resumes once they answer.
"Something" includes a choice between options, an acknowledgement of an
unavoidable outcome, a free-form input (e.g. a dice roll), or any other place
the engine has to stop and wait. The framework is one mechanism for all of
them.

**Status:** design, pre-implementation. The pieces this depends on
(`GameCacheModel`, `EventReceipt`, the snapshot loop) already exist.

> The filename is `choice-events.md` for legacy reasons. The framework covers
> prompts in general; a choice is one kind of prompt.

---

## 1. The Constraint That Shapes Everything

A pending prompt **does not survive a server restart**. The only thing that
survives a restart is the `GameModel` snapshot inside `GameCacheModel`. The
prompt, its `TaskCompletionSource`, and the per-turn event list are all
in-memory.

This is what makes the framework below tractable. It means:

1. The engine can `await` a response inline rather than serialising a
   continuation.
2. A turn that is interrupted by a restart is lost — the player whose turn it
   was re-enters their roll. The previous turn's snapshot is intact, so no
   committed state is at risk.
3. Replay and backtrack continue to operate on the snapshot timeline, not on
   the prompt stream — prompts are *inputs* to a turn, not part of its
   permanent record.

This contract aligns with `game-engine.md` §8 (snapshot at the *start* of each
turn) and §9 (timeline of snapshots, not events).

---

## 2. Prompts are mid-execution only

Prompts are **not** the universal player-input mechanism. They exist for one
narrow purpose: pausing the engine *mid-execution* when it needs a player
decision to continue or branch.

Anything a player initiates while the engine is **not** running — at turn
boundaries, during their idle pre-roll window, or after their turn — is a
**player-initiated command**, not a prompt. Commands and prompts are
complementary, not interchangeable:

| Direction | Mechanism | Triggered when |
|---|---|---|
| Engine → player | Prompt | The engine is mid-execution and needs input to proceed |
| Player → engine | Command (POST / SignalR method) | The player decides to do something while the engine is idle |

- **Prompts** (engine pauses, player responds): dice roll during a turn,
  auction bid, Free Parking hand-in decision, NOPE-window response, "you
  can't afford this" acknowledgement.
- **Commands** (player acts at turn-idle or boundary): build a house, sell
  a property, mortgage / unmortgage, propose or accept a deal, choose how to
  leave jail, declare voluntary bankruptcy, end turn.

Why the distinction:

1. **The framework's value is the pause.** With no execution running, there
   is no pause to introduce; a prompt would be ceremony.
2. **Idle-time actions are open-ended sequences.** A player can build, then
   deal, then sell, then end-turn — possibly all four before rolling. The
   singular-pending-prompt invariant (§4) cannot represent that; commands
   naturally do.
3. **Intent direction matters.** Prompts express "the engine needs to know X
   to continue". Commands express "the player wants to do Y". Conflating
   them blurs the engine state machine.

The prompt framework is consumed by exactly one part of the engine: the
**turn-execution loop**. Everything else (building, dealing, mortgaging,
leaving jail, ending the turn, voluntary surrender outside execution) lives
in the command pipeline — a separate system, not covered by this doc.

A few cases that look ambiguous but aren't:

- **Jail exit method.** The *choice* (pay-fee / play-card / attempt-double)
  is a command at turn start, before any execution. If the player picks
  attempt-double, the engine then starts the turn and a `DiceRollPrompt`
  opens *during* execution. The command initiates the engine; the prompt
  is what the engine emits while running.
- **Voluntary bankruptcy.** "At any time" means it appears both as a response
  option *within* a mid-execution shortfall prompt and as a separate
  surrender command at turn-idle — different contexts, same intent.
- **Auction bidding.** A prompt, not a command. The auction is mid-execution
  (a loop the engine is running), and each bidder gets a sequential prompt.

---

## 3. What a Prompt Is

A **prompt** is anything the engine emits that pauses execution until a
response comes back. The framework does not distinguish between sub-kinds — a
prompt is just `Prompt<TResponse>`. Common shapes:

1. **Acknowledgement.** Single "OK" response. Used when the rules force a
   single outcome but the player needs to be informed before it happens.
   Example: a player with £20 lands on Vine Street (£200). The rules require
   an auction — there is no decision to make. The engine emits an acknowledge
   prompt ("You cannot afford this property. An auction will begin.") and on
   "OK" proceeds straight into the auction.

2. **Choice.** Two or more options, one of which the player picks. The classic
   case — buy / decline, jail-exit method, loan vs mortgage.

3. **Input.** A typed value the player provides. The dice roll is the
   canonical example: the engine emits a `DiceRollPrompt`, the player enters
   the three dice values, the engine resumes with them.

4. **Interruptible window.** A specialised pattern for card interrupts (NOPE
   and friends) — see §9. Two response paths (host Continue or any eligible
   holder's card play), no timeout, the table decides when to close it.

All four use the same `Prompt<TResponse>` / `PromptResponse` / `IPromptProvider`
mechanism. Sub-kinds are concrete subclasses, not separate frameworks.

---

## 4. Where State Lives

| Lives in | Survives restart? | Holds |
|---|---|---|
| `GameModel` | yes (DB snapshot) | committed game state only |
| `GameCacheModel.PendingPrompt` | **no** (in-memory) | the open prompt + its `TaskCompletionSource` |
| `GameCacheModel.Events` | no (in-memory, per turn) | event receipts |
| `GameCacheModel.ConcurrencyStamp` | no | guards stale submissions |

1. **`PendingPrompt` slot is singular.** At most one prompt is open at any
   moment. Auctions and interrupt chains are sequences of single prompts, not
   a stack of concurrent ones — see §8 and §9.

2. **Setting and clearing `PendingPrompt` re-stamps `ConcurrencyStamp`.** Same
   pattern the cache already uses for `AddEvent` and `SaveChanges`. A response
   submitted against an old stamp is rejected.

---

## 5. Types

Strongly typed pairing of prompt to response. The engine sees the typed pair;
the wire sees polymorphic JSON via `[JsonPolymorphic]` discriminators,
mirroring the existing `EventReceipt` setup.

```csharp
public abstract class Prompt
{
    public string PromptId { get; init; } = Guid.NewGuid().ToString();
    public string PlayerId { get; init; } = "";       // subject — see note below
    public abstract PromptTarget Target { get; }       // audience — distinct from subject
    public TimeSpan? Timeout { get; init; }            // advisory — see §10
    public PromptResponse? DefaultResponse { get; init; }
    public string Title { get; init; } = "";          // every prompt has these
    public string Body { get; init; } = "";
}

public abstract class Prompt<TResponse> : Prompt
    where TResponse : PromptResponse { }

public abstract class PromptResponse
{
    public string PromptId { get; init; } = "";       // must match the open prompt
}

public sealed record PromptTarget(
    PromptTargetKind Kind,
    IReadOnlyList<string> PlayerIds)
{
    public static PromptTarget SinglePlayer(string id) =>
        new(PromptTargetKind.Single, [id]);

    public static PromptTarget Group(IEnumerable<string> ids) =>
        new(PromptTargetKind.Group, ids.ToArray());
}

public enum PromptTargetKind
{
    Single,
    Group
}
```

A choice prompt:

```csharp
public sealed class AcquirePropertyPrompt : Prompt<AcquirePropertyResponse>
{
    public ushort BoardIndex { get; init; }
    public uint Cost { get; init; }
    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
    // PlayerId / Title / Body inherited from the Prompt base.
}

public sealed class AcquirePropertyResponse : PromptResponse
{
    public bool Accept { get; init; }   // engine decides whether "yes" = buy or reserve
}
```

An acknowledgement prompt:

```csharp
public sealed class AcknowledgePrompt : Prompt<AcknowledgeResponse>
{
    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
    // PlayerId / Title / Body inherited.
}

public sealed class AcknowledgeResponse : PromptResponse { }   // intentionally empty
```

An input prompt:

```csharp
public sealed class DiceRollPrompt : Prompt<DiceRollResponse>
{
    public ushort DiceCount { get; init; }               // 1, 2, or 3
    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}

public sealed class DiceRollResponse : PromptResponse
{
    public ushort Die1 { get; init; }
    public ushort? Die2 { get; init; }                   // populated when DiceCount >= 2
    public ushort? ThirdDie { get; init; }               // populated when DiceCount == 3
}
```

**`PlayerId` on the base — subject, not audience.** Every prompt carries a
`PlayerId`: the player the prompt is *about*. For single-player prompts
(everything but `InterruptibleWindowPrompt`) the subject also happens to be
the audience, and `Target` is just `SinglePlayer(PlayerId)`. For
group-targeted prompts the subject and audience diverge — on
`InterruptibleWindowPrompt`, `PlayerId` is the player whose card play the
window is interrupting, while `Target` is the group of eligible responders
derived from `EligiblePlays`. Authorisation is enforced by the validator
against the response and the cache, not by either field.

**One operation, one prompt — to the subject only, never affected
counterparties.** When an operation touches more than one player, the prompt
goes solely to the player it acts *on*, not to those merely affected. Player B
landing on Player A's property gets the rent prompt (an `AcknowledgePrompt`, or
a `ShortfallPrompt` if they can't pay, or a card prompt if a card intervenes);
Player A, the collector, gets **no** prompt. This is the helper-not-simulator
principle (`game-engine.md` §1): the players share one physical table, so
stopping everyone for the same operation would only slow play — the engine
pauses the *one* person whose input it needs. Affected counterparties learn of
the outcome through the live state broadcast (`web-orchestration.md` §6), not a
prompt — Player A's balance simply updates when the rent applies.

The host identity is not carried on any prompt — it lives on
`GameCacheModel.HostPlayerId` (sourced from the game's DTO). The validator
reads it from there when enforcing host-bypass authorisation, so every prompt
that the host can submit on a player's behalf does so without explicitly
carrying the host id.

1. **`Target` describes the audience, not authorisation.** Who is allowed to
   submit which response variant is enforced by `PromptValidator` against the
   specific response and game state — not by the target. This matters for
   interruptible windows (§9), where Continue is host-only but a card-play
   response can come from any eligible holder.

2. **The response always carries its prompt's `PromptId`.** The provider
   checks it on submit. This catches a client submitting against a prompt
   that has since been replaced.

3. **`AcknowledgeResponse` carries nothing but the id.** A prompt with no
   meaningful response payload is fine — the value is the pause itself.

---

## 6. `IPromptProvider` — the seam

One interface separates the engine from the outside world. The engine awaits;
the web layer submits.

```csharp
public interface IPromptProvider
{
    // Engine-side. Awaited inside turn logic.
    Task<TResponse> RequestAsync<TResponse>(
        Prompt<TResponse> prompt,
        CancellationToken ct = default)
        where TResponse : PromptResponse;

    // Web/SignalR-side. Returns false if stale or invalid (no exception).
    bool TrySubmit(string submittingUserId, string concurrencyStamp, PromptResponse response);
}
```

Implementation shape:

```csharp
internal sealed class PromptProvider(GameCacheModel cache) : IPromptProvider
{
    public Task<TResponse> RequestAsync<TResponse>(
        Prompt<TResponse> prompt, CancellationToken ct)
        where TResponse : PromptResponse
    {
        var tcs = new TaskCompletionSource<PromptResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        cache.SetPendingPrompt(new PendingPrompt(prompt, tcs));  // re-stamps
        ct.Register(() => tcs.TrySetCanceled());

        return tcs.Task.ContinueWith(t => (TResponse)t.Result, ct);
    }

    public bool TrySubmit(string submittingUserId, string concurrencyStamp, PromptResponse response)
    {
        if (cache.ConcurrencyStamp != concurrencyStamp) return false;

        var pending = cache.PendingPrompt;
        if (pending is null) return false;
        if (pending.Prompt.PromptId != response.PromptId) return false;
        if (!PromptValidator.Validate(pending.Prompt, response, submittingUserId, cache)) return false;

        cache.ClearPendingPrompt();                              // re-stamps
        pending.Tcs.TrySetResult(response);
        return true;
    }
}

internal sealed record PendingPrompt(
    Prompt Prompt,
    TaskCompletionSource<PromptResponse> Tcs);
```

1. **`TrySubmit` never throws.** All failure modes return `false`: stale
   stamp, no pending prompt, mismatched id, invalid response, unauthorised
   submitter. The web layer treats `false` as "your view is out of date,
   refresh and re-render".

2. **`PromptValidator.Validate` is data-driven and identity-aware.** It takes
   the prompt, the response, the submitting user, and the current game state.
   Different response variants of the same prompt can have different
   authorised submitters — host-only for Continue, any-eligible-holder for a
   card-play response on the same prompt. New prompt types add a validator
   branch; the provider does not change. Acknowledge prompts validate
   trivially.

3. **Cancellation cancels the awaiter.** Passing a cancelled
   `CancellationToken` (e.g. on game cancellation) trips `TrySetCanceled` on
   the TCS; whatever `await` is sitting inside the engine throws
   `OperationCanceledException`, unwinding the turn.

---

## 7. How the engine reads — async all the way down

Rule code is plain async. A prompt looks like an ordinary await.

**Choice:**

```csharp
private async Task ResolveLandingOnUnownedProperty(
    PlayerModel p, PropertyModel prop, CancellationToken ct)
{
    if (CanAffordOutright(p, prop))
    {
        var resp = await prompts.RequestAsync(new AcquirePropertyPrompt
        {
            PlayerId = p.Id,
            BoardIndex = prop.BoardIndex,
            Cost = prop.PurchaseCost,
            Title = $"Buy {prop.Name}?",
            Body = $"Spend £{prop.PurchaseCost} to acquire {prop.Name}.",
            Timeout = TimeSpan.FromMinutes(2),
            DefaultResponse = new AcquirePropertyResponse { Accept = false },
        }, ct);

        if (resp.Accept) { await ExecutePurchase(p, prop, ct); return; }
        await BeginAuction(prop, ct);
        return;
    }

    // Rules force an auction. Tell the player, then proceed.
    await prompts.RequestAsync(new AcknowledgePrompt
    {
        PlayerId = p.Id,
        Title = "Cannot afford",
        Body = $"You cannot afford {prop.Name}. An auction will begin.",
        Timeout = TimeSpan.FromSeconds(30),
        DefaultResponse = new AcknowledgeResponse(),
    }, ct);

    await BeginAuction(prop, ct);
}
```

**Input:**

```csharp
var dice = await prompts.RequestAsync(new DiceRollPrompt
{
    PlayerId = p.Id, DiceCount = 3,
    Title = "Your turn",
}, ct);

// DiceCount == 3 ⇒ validator has guaranteed Die2 and ThirdDie are populated.
await ResolveRoll(p, dice.Die1, dice.Die2!.Value, dice.ThirdDie!.Value, ct);
```

1. **Nested prompts are nested awaits.** Buy → can't afford → loan-or-mortgage
   is three awaits in a row. NOPE on a card → NOPE on the NOPE is a loop
   until no NOPE comes back (see §9). The C# state machine generated by
   `async/await` is the "continuation".

2. **Acknowledgements cost an extra await but no extra mechanism.** They
   exist purely for UX — the rules engine would proceed without them, but the
   player needs to know what's about to happen.

3. **A prompt with no `DefaultResponse` blocks indefinitely.** For
   acknowledgements that must be seen, and for the interruptible window in
   §9, omit the default; the engine waits until a real response arrives.

---

## 8. Multi-player prompts

Two patterns, no extra framework primitives.

1. **Single player** — the default, `PromptTarget.SinglePlayer(id)`.

2. **Sequential players** — a loop of single-player prompts in the engine:
   ```csharp
   foreach (var bidder in turnOrder)
       bids[bidder.Id] = await prompts.RequestAsync(
           new AuctionBidPrompt { PlayerId = bidder.Id, ... }, ct);
   ```

"First responder wins" behaviour falls out of the framework automatically:
the concurrency stamp re-stamps whenever the cache clears the prompt, so the
second submitter against a single prompt always loses. No special target
kind is required. The interruptible window (§9) uses this for card plays
while keeping Continue authority host-only.

---

## 9. Interruptible windows — the card-interrupt pattern

The canonical answer to "what happens when a player plays a card that
another player's held card could cancel". NOPE is the headline case; any
future response card uses the same shape.

### Principle

This is a **helper, not a simulator** (`game-engine.md` §1). The NOPE chat
happens around the physical table — "wait, hold on, I want to NOPE that".
The app's job is to record the outcome, not to orchestrate the moment. So
the engine pauses and the **host** (the tablet controller) decides when the
window is closed, by tapping Continue. No timers, no per-player polling.

### Shape

One prompt, one response, two action paths:

```csharp
public sealed class InterruptibleWindowPrompt : Prompt<InterruptibleWindowResponse>
{
    public IReadOnlyList<EligibleCardPlay> EligiblePlays { get; init; } = [];
    public override PromptTarget Target =>
        PromptTarget.Group(EligiblePlays.Select(e => e.PlayerId).Distinct());
    // Title/Body inherited.
}

public sealed record EligibleCardPlay(string PlayerId, string CardId, string CardName);

public sealed class InterruptibleWindowResponse : PromptResponse
{
    public InterruptAction Action { get; init; }                // Continue | PlayCard
    public string? PlayedByPlayerId { get; init; }
    public string? PlayedCardId { get; init; }
}

public enum InterruptAction { Continue, PlayCard }
```

`Timeout` is always `null` for this prompt — no auto-resolve, ever. The host
taps Continue when the table is ready.

### Authorisation

| Response action | Who may submit | Notes |
|---|---|---|
| `Continue` | The host only (resolved from `GameCacheModel.HostPlayerId`) | No race with NOPE-holders. |
| `PlayCard` | The player named in `EligiblePlays` whose `CardId` is being played — or the host on their behalf | Phone or via the host tablet. |

The validator enforces both, against the prompt's data, the submitter's
identity, and the cache's host id (§6 rule 2).

### The chain

```csharp
async Task PlayCardWithInterrupts(CardPlay play, CancellationToken ct)
{
    while (true)
    {
        var eligible = FindEligibleResponses(play);
        if (eligible.Count == 0) break;                  // no one can interrupt → skip the window

        var resp = await prompts.RequestAsync(new InterruptibleWindowPrompt
        {
            Title = "Response window",
            Body = DescribePlay(play),
            EligiblePlays = eligible,
            Timeout = null,
            DefaultResponse = null,
        }, ct);

        if (resp.Action == InterruptAction.Continue) break;

        play = await RecordCardPlay(resp.PlayedByPlayerId!, resp.PlayedCardId!, ct);
        // loop with the new play; the loop opens the next window if responders remain
    }

    await ApplyCardEffect(play, ct);
}
```

A NOPE chain across four players (A plays, B NOPEs, A re-NOPEs with their
second NOPE, no one re-NOPEs, A's NOPE stands → original card is cancelled
→ B's NOPE cancelled the cancel → A's original card resolves) is three
iterations of the loop. The engine holds no chain-specific state; the stack
of card plays lives in the per-turn `Events` list as `CardPlayedReceipt`s.

### Skipping the window

If no other player holds an eligible response, the engine skips the prompt
entirely and applies the effect directly. Most card plays will hit this
path. No event receipt is written for the absent window — the receipt for
the card play itself is the record.

### Cards played outside an open window

Some cards are playable "at any time" (`game-rules.md` Cards). When such a
card is played with no current prompt open, the same flow runs: validate
the play, enter the loop above, applying or chaining as the eligible-response
set demands.

A response-only card (e.g. NOPE) played outside an open window is rejected
at the command layer — there is nothing to cancel.

### The host's two surfaces

The host player is connected twice: once as the tablet controller, once as
their own phone-side player profile. Both connections sit inside the
SignalR group `game-{gameId}` (`signalr-design.md`). The `PromptOpened`
event reaches both. The tablet renders the Continue button and the full
`EligiblePlays` list (the host can play any player's card on their behalf,
e.g. via a sidebar player-profile view). The phone shows only that player's
own eligible plays — no Continue button.

How exactly the tablet lays this out (sidebar profile view, control panel
position, whether non-holders' phones display anything) is a UI decision,
not framework. The framework guarantee is: the prompt reaches every
connection in the group, validation enforces authority on submit.

---

## 10. Timeouts and default responses are advisory

The framework does not run timers — the engine stays deterministic and
side-effect-free.

1. **`Prompt.Timeout` and `Prompt.DefaultResponse` are hints to the web layer.**
   When the prompt is opened, the web layer schedules a timer; when it fires,
   it calls `TrySubmit(stamp, DefaultResponse)`. As far as the engine knows,
   that submission is identical to a player clicking the default.

2. **A `Timeout` without a `DefaultResponse` means the prompt never auto-resolves.**
   Useful for prompts where there is no sensible default (e.g. "your turn —
   roll", or the interruptible window in §9).

3. **Time is therefore outside the engine.** Snapshot and replay continue to
   work the same way: replays read events, not timers.

---

## 11. SignalR surface

Two new server-to-client events on `GameHub`, one client-to-server method.
Scoped to the existing `game-{gameId}` group from `signalr-design.md`.

| Direction | Name | Payload | Notes |
|---|---|---|---|
| S → C | `PromptOpened` | polymorphic `Prompt` + `concurrencyStamp` | Broadcast to the group; clients filter by `Target.PlayerIds` and validation rules to decide what to render |
| S → C | `PromptClosed` | `promptId`, `concurrencyStamp` | After `TrySubmit` succeeds or the default fires |
| C → S | `SubmitPrompt` | `concurrencyStamp`, polymorphic `PromptResponse` | Server calls `IPromptProvider.TrySubmit` with the calling user's id |

1. **`PromptOpened` broadcasts to the whole group.** Every connection sees
   it — tablet, phones, and the host's *two* connections (tablet + their own
   phone). Clients decide what to render based on whether the calling user
   is in `Target.PlayerIds` and what response variants they are authorised
   to submit.

2. **Late joiners and reconnects pull the current state.** A
   `GetCurrentPrompt` hub method (or the existing game-state fetch,
   augmented) returns the open prompt if one exists, or `null`. Reconnecting
   phones therefore see the prompt they missed.

---

## 12. Restart contract

1. **`PendingPrompt == null` after a restart, always.** The cache rehydrates
   from the snapshot — no prompt, no TCS, no events.

2. **The current player re-enters the lost turn.** Their UI returns to the
   pre-roll state. The dice they re-enter may differ from the dice they
   entered in the lost turn; that is accepted — nothing about the lost turn
   was committed.

3. **The web layer should treat a missing pending prompt as "no prompt".**
   Not "something went wrong". A reconnecting client asking
   `GetCurrentPrompt` and receiving `null` should render the normal turn UI.

---

## 13. What this framework gives up

Recorded so the trade is explicit.

1. **No mid-turn crash recovery.** By design — see §1. If this is ever
   needed, it would be added by persisting `(promptId, response)` pairs into
   the next turn's snapshot and replaying them on load. That is **not** part
   of this framework.

2. **No backtrack into the middle of a turn.** Only between turns. Matches
   `game-engine.md` §9.

3. **The engine becomes async end-to-end.** Every method that can reach a
   prompt (most of them, transitively) is `async Task`. The alternative — an
   explicit phase machine with one command per response — is harder to write
   rule logic in and is only worth the cost if mid-turn persistence is needed.
   It isn't.

---

## 14. Traceability

1. **`game-rules.md`** — defines the situations that require a prompt (buy /
   decline, auction bidding, Free Parking hand-in, NOPE, and all the
   acknowledgement points where rules force an outcome the player should be
   told about). Situations handled by *commands* (build, sell, deal, leave
   jail, end turn, etc.) are not prompts — see §2.
2. **`game-engine.md`** — defines the surrounding engine architecture.
3. **`choice-events.md`** (this doc) — defines the framework prompts flow
   through, with the interruptible-window pattern (§9) as the canonical
   card-interrupt shape.
4. **Engine tests** — each `Prompt<TResponse>` type gets unit tests for:
   prompt emission under the right conditions, validation of valid and
   invalid responses, authorisation of the submitter for each response
   variant, and the engine's behaviour on each branch of the response.

---

## 15. Prompt Types Plan

Catalogue of concrete `Prompt<TResponse>` types. Each entry pins the
discriminator string used on the wire and the authorisation rules the
validator enforces. Update this section whenever a prompt type is added,
renamed, or retired.

**Every entry below is a mid-execution prompt** (per §2). Player-initiated
commands at turn boundaries (build, sell, deal, leave jail, end turn, etc.)
are out of scope for this catalogue — they live in the command pipeline.

**Host identity is not a prompt field.** It lives on
`GameCacheModel.HostPlayerId` (sourced from `GameDTO` at cache
construction). Every prompt's "host can submit on a player's behalf" rule is
enforced by the validator reading the cache, not by the prompt carrying the
host id. None of the prompts listed below have a `HostPlayerId` field — the
authorisation sections refer to "the host" implicitly via the cache.

### 15.1 `InterruptibleWindowPrompt` — *implemented*

The card-interrupt pattern. See §9 for the full design discussion.

|  |  |
|---|---|
| Discriminator | `InterruptibleWindow` |
| Target | Group — each eligible-play player. (The host sees it via the SignalR group broadcast, no need to be in `Target.PlayerIds`.) |
| Timeout | Always `null` (the table decides when the window closes) |
| Response | `InterruptibleWindowResponse` |
| Files | `Models/Prompts/PromptTypes/InterruptibleWindowPrompt.cs`, `Models/Prompts/PromptTypes/Responses/InterruptibleWindowResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the *initiator* of the play this window is
  interrupting (the card-player whose play is on the line). Subject of the
  prompt, distinct from the audience. Useful for rendering ("Player X
  played card Y — anyone NOPE?").
- `EligiblePlays: IReadOnlyList<EligibleCardPlay>` — the `(PlayerId, CardId,
  CardName)` triplets that may be submitted as `PlayCard` responses.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

- `Action: InterruptAction` — `Continue` or `PlayCard`.
- `PlayedByPlayerId`, `PlayedCardId` — required when `Action == PlayCard`,
  ignored otherwise.

**Authorisation**

- `Continue` → submitter must equal `cache.HostPlayerId`.
- `PlayCard` → the `(PlayedByPlayerId, PlayedCardId)` pair must match an
  entry in `EligiblePlays`; submitter must be either `PlayedByPlayerId` or
  `cache.HostPlayerId` (host can play any player's card on their behalf via
  the tablet).

### 15.2 `AcknowledgePrompt` — *implemented*

Single-OK notification — pauses the engine until the named player
acknowledges an unavoidable outcome. The "fancy notification" shape from §3
case 1.

|  |  |
|---|---|
| Discriminator | `Acknowledge` |
| Target | Single player (`PlayerId`) |
| Timeout | Caller-controlled; combine with a `DefaultResponse` of `new AcknowledgeResponse()` for auto-dismiss |
| Response | `AcknowledgeResponse` — empty; the pause itself is the value |
| Files | `Models/Prompts/PromptTypes/AcknowledgePrompt.cs`, `Models/Prompts/PromptTypes/Responses/AcknowledgeResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the named target who taps OK.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

None. Only the `PromptId` inherited from the base is carried.

**Authorisation**

Submitter must equal `PlayerId` or `cache.HostPlayerId` (host dismisses on
the player's behalf via the tablet).

### 15.3 `DiceRollPrompt` — *implemented*

Input prompt for entering physical dice values. Covers the standard turn roll
(3 dice) and card-forced rolls of variable arity (1, 2, or 3 dice). One of
the four prompt shapes named in §3 (input).

|  |  |
|---|---|
| Discriminator | `DiceRoll` |
| Target | Single player (`PlayerId`) |
| Timeout | Typically `null` — the player rolls when they roll |
| Response | `DiceRollResponse` |
| Files | `Models/Prompts/PromptTypes/DiceRollPrompt.cs`, `Models/Prompts/PromptTypes/Responses/DiceRollResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the player rolling.
- `DiceCount: ushort` — how many dice to enter; must be 1, 2, or 3. The
  response must populate exactly that many of `Die1` / `Die2` / `ThirdDie`.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

- `Die1: ushort` — always required.
- `Die2: ushort?` — required when `DiceCount >= 2`, must be null otherwise.
- `ThirdDie: ushort?` — required when `DiceCount == 3`, must be null
  otherwise. Explicitly named (rather than relying on list-order convention)
  so the consumer can never mistake which die is the third.

**Authorisation and validation**

- Submitter must equal `PlayerId` or `cache.HostPlayerId` (host can submit
  dice on the player's behalf via the tablet).
- `DiceCount` must be 1, 2, or 3.
- Population of `Die1` / `Die2` / `ThirdDie` must match `DiceCount` exactly —
  no extra populated fields, no missing required fields.
- Every populated die value must be in the range 1–6.

**Out of scope**

Setup dice (turn-order rolls before the game starts) go through the setup
hub, not the engine. No `DiceRollPrompt` is involved.

### 15.4 `AcquirePropertyPrompt` — *implemented*

Asks the lander whether they want to take a property they've landed on. The
prompt is deliberately a binary yes/no — whether "yes" leads to a standard
buy or a reservation under the reserve rule (`game-rules.md` Reserved
Properties) is engine state. The engine that *creates* the prompt knows the
mode and crafts the appropriate Title/Body; the engine that *handles* the
response branches on game state to either `MarkPropertyOwned` or
`MarkPropertyReserved`. Framework stays oblivious.

|  |  |
|---|---|
| Discriminator | `AcquireProperty` |
| Target | Single player (`PlayerId`) |
| Timeout | Caller-controlled |
| Response | `AcquirePropertyResponse` |
| Files | `Models/Prompts/PromptTypes/AcquirePropertyPrompt.cs`, `Models/Prompts/PromptTypes/Responses/AcquirePropertyResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the lander.
- `BoardIndex: ushort` — the property's board index. Set/colour resolves via
  `PropertySetHelper.ResolveColour(ushort)`.
- `Cost: uint` — what the lander would pay for the offered action (full price
  for a buy, half price for a reserve). The engine computes this; the
  framework does not interpret it.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

- `Accept: bool` — `true` = take it (buy or reserve, engine decides), `false`
  = decline (→ auction).

**Authorisation**

Submitter must equal `PlayerId` or `cache.HostPlayerId` (host can submit on
the lander's behalf via the tablet).

**Notes**

- Affordability is gated *before* this prompt opens. If the lander can't
  afford the offered action, the engine emits an `AcknowledgePrompt`
  ("can't afford, auction begins") and skips this prompt entirely.
- The prompt does *not* carry a "this is a reserve" flag. That information
  lives in the engine's local state when constructing the prompt and again
  when handling the response. Title/Body carry whatever the player needs to
  see ("Reserve Pall Mall for £70 — completes pink set").

### 15.5 `TargetPlayerPrompt` — *implemented*

Generic player selector. Reused by any engine path that needs the chooser
to pick one or more players — card effects, deal initiation, etc. The
framework deliberately does not describe the intent; the call site sets
`Title` / `Body` and `Count`. `Count` is fixed by the caller, not offered
as a range — the player never decides how many to pick. Where two cards in
the same deck differ in how many targets they affect (e.g. one card
targets one player, another targets two), they remain separate cards with
their own fixed counts; each card's handler opens this prompt with its
own concrete `Count`.

|  |  |
|---|---|
| Discriminator | `TargetPlayer` |
| Target | Single player (`PlayerId`) |
| Timeout | Caller-controlled |
| Response | `TargetPlayerResponse` |
| Files | `Models/Prompts/PromptTypes/TargetPlayerPrompt.cs`, `Models/Prompts/PromptTypes/Responses/TargetPlayerResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the chooser.
- `EligiblePlayerIds: IReadOnlyList<string>` — the candidate set the chooser
  must pick from. The engine populates this per context.
- `Count: ushort` — how many players must be selected. Fixed by the caller.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

- `SelectedPlayerIds: IReadOnlyList<string>` — must have length `Count`,
  every id in `EligiblePlayerIds`, and no duplicates.

**Authorisation**

Submitter must equal `PlayerId` or `cache.HostPlayerId` (host can submit on
the chooser's behalf via the tablet).

### 15.6 `TargetPropertyPrompt` — *implemented*

Generic property selector. Reused for any property-pick context —
mortgaging to cover a shortfall, selling a building, handing a property into
Free Parking, choosing which of an opponent's properties to purge, and so
on. Same intent-agnostic shape as `TargetPlayerPrompt`.

|  |  |
|---|---|
| Discriminator | `TargetProperty` |
| Target | Single player (`PlayerId`) |
| Timeout | Caller-controlled |
| Response | `TargetPropertyResponse` |
| Files | `Models/Prompts/PromptTypes/TargetPropertyPrompt.cs`, `Models/Prompts/PromptTypes/Responses/TargetPropertyResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the chooser.
- `EligibleBoardIndexes: IReadOnlyList<ushort>` — the candidate set, by
  `PropertyModel.BoardIndex`. The engine populates per context (e.g. for
  mortgaging, only the player's currently unmortgaged properties).
- `Count: ushort` — how many properties must be selected. Fixed by the
  caller. For actions that need per-step rule re-evaluation (selling a
  building one at a time so each sale runs through the even-building rule,
  for example), the engine loops with `Count = 1` and re-derives the
  eligible set each iteration.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

- `SelectedBoardIndexes: IReadOnlyList<ushort>` — must have length `Count`,
  every index in `EligibleBoardIndexes`, and no duplicates.

**Authorisation**

Submitter must equal `PlayerId` or `cache.HostPlayerId`.

### 15.7 `ShortfallPrompt` — *implemented*

Opens when the engine has computed a payment the player cannot meet from
their cash on hand. The response is a single `ShortfallAction` — the
player's chosen way to raise the balance (loan, mortgage, sell buildings,
propose a settling deal) or to surrender (declare bankruptcy). See
`game-rules.md` Default rule 7, Loans, Mortgaging, Bankruptcy.

|  |  |
|---|---|
| Discriminator | `Shortfall` |
| Target | Single player (`PlayerId`) |
| Timeout | Caller-controlled |
| Response | `ShortfallResponse` |
| Files | `Models/Prompts/PromptTypes/ShortfallPrompt.cs`, `Models/Prompts/PromptTypes/Responses/ShortfallResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the player who owes the money.
- `Cost: uint` — the total amount the player has to pay.
- `PlayerBalance: uint` — the player's available cash at the moment the
  shortfall is computed.
- `AmountOwed: uint` — computed as `Cost - PlayerBalance`. Get-only:
  serialised on the way out so the frontend receives the shortfall
  pre-computed, skipped on deserialisation so tampered values can't poison
  the prompt (the server always re-derives from `Cost` and `PlayerBalance`).
- `OwedToPlayerId: string?` — the creditor, if the debt is owed to another
  player. `null` when the debt is owed to the bank, in which case
  `ProposeDeal` is not a valid response.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

- `Action: ShortfallAction` — one of `TakeLoan` / `Mortgage` /
  `SellHouses` / `ProposeDeal` / `DeclareBankruptcy`.

**Authorisation and validation**

- Submitter must equal `PlayerId` or `cache.HostPlayerId`.
- `ProposeDeal` is rejected when `OwedToPlayerId` is `null` — settling-deal
  semantics require a creditor to deal with.
- Other actions are accepted at the framework level. The engine is
  responsible for handling whether the chosen path is actually achievable
  (sufficient buildings to sell, loan slots available, etc.) and for
  re-opening the prompt if the player is still short afterwards.

**Notes**

- The deal-proposal flow itself (offer items, accept / reject / counter,
  debt-cancellation semantics) is a separate sub-system not yet designed.
  Until it lands, `ProposeDeal` is enumerated on the response but no engine
  path consumes it.
- `SellHouses` is shortfall-only — `game-rules.md` Default rule 7 forbids
  raising funds via mortgaging, selling buildings, or non-creditor deals
  for a new commitment (buy / bid). Buying and bidding must be paid from
  money the player genuinely has.

### 15.8 `AuctionBidPrompt` — *implemented*

Sequential bidding prompt for the auction loop triggered by `game-rules.md`
Default rule 6 (declined or unaffordable purchases). The engine opens one
of these per bidder per round, in clockwise order, until the auction
resolves. Every player may bid, including the player who declined the
purchase and any players currently in jail (Default rule 6).

|  |  |
|---|---|
| Discriminator | `AuctionBid` |
| Target | Single player (`PlayerId`) |
| Timeout | Caller-controlled |
| Response | `AuctionBidResponse` |
| Files | `Models/Prompts/PromptTypes/AuctionBidPrompt.cs`, `Models/Prompts/PromptTypes/Responses/AuctionBidResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the bidder being asked.
- `BoardIndex: ushort` — the property being auctioned.
- `CurrentHighBid: uint` — the highest bid so far. `0` before the first
  bid; a new bid must strictly exceed this.
- `CurrentHighBidderId: string?` — the player currently winning, if any.
  Informational for the frontend; not consulted by validation.
- `PlayerBalance: uint` — the bidder's available cash. Bids cannot exceed
  this — `game-rules.md` Default rule 7 bars raising funds to bid.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

- `Action: AuctionBidAction` — `Bid` or `Pass`.
- `BidAmount: uint?` — required when `Action == Bid`; must be `null` when
  `Action == Pass`.

**Authorisation and validation**

- Submitter must equal `PlayerId` or `cache.HostPlayerId`.
- `Bid` → `BidAmount` must be present, strictly greater than
  `CurrentHighBid`, and not greater than `PlayerBalance`.
- `Pass` → `BidAmount` must be `null`.

### 15.9 `CardOptionPrompt` — *implemented*

Generic n-ary selector for card handlers that present a labelled choice
("pay £200 OR draw a Chance card", "advance to nearest station OR pay
£50"). The framework does not model per-card effects — each card has its
own handler per `game-engine.md` §11 — but the option-pick shape is shared.
Composes naturally with `TargetPlayerPrompt` and `TargetPropertyPrompt`
when an option needs follow-on targeting.

|  |  |
|---|---|
| Discriminator | `CardOption` |
| Target | Single player (`PlayerId`) |
| Timeout | Caller-controlled |
| Response | `CardOptionResponse` |
| Files | `Models/Prompts/PromptTypes/CardOptionPrompt.cs`, `Models/Prompts/PromptTypes/Responses/CardOptionResponse.cs` |

**Prompt fields**

- `PlayerId` (inherited) — the player choosing.
- `Options: IReadOnlyList<CardOption>` — `(Key, Label)` records. `Key` is a
  stable identifier returned in the response; `Label` is the player-facing
  display text. By convention at least two options — a single-option list
  is a non-choice and should not open this prompt.
- `Title`, `Body` (inherited) — from the `Prompt` base.

**Response payload**

- `SelectedKey: string` — must match the `Key` of one of the prompt's
  `Options`.

**Authorisation**

Submitter must equal `PlayerId` or `cache.HostPlayerId`.

**Notes**

- Keys (not list indexes) carry the choice so logs and audit trails remain
  readable, and so a later card revision that reorders options doesn't
  silently change the meaning of past responses.

### Planned

Future prompt types are listed here as they are designed. Status moves from
*planned* to *implemented* once the prompt, response, validator branch, and
`[JsonDerivedType]` discriminators are landed.

— *(none currently planned; the next is added when discussed)*
