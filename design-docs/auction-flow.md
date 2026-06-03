# Auctions — Flow & Resolution

How a property goes to auction, how bidding runs, and how the winner is
settled. Auctions are a mid-execution prompt loop driven by the engine and
gated by the prompt framework — they pair with `AuctionBidPrompt`
(`choice-events.md` §15.8), which owns the per-bid contract while this doc
owns the *flow*.

**Status:** partial. `AuctionService.RunAuction` — the bid loop and settlement
described here — is built (`MP.GameEngine/Services/SubSystems/AuctionService.cs`),
on top of `AuctionBidPrompt`, `MoneyHelper.MinAuctionBid` / `AuctionIncrements`,
`TransactionService.WinAuction`, `GameModel.GetPlayers` and
`PropertyModel.OwnProperty`. Not yet built: the `BoardService` landing hook
that triggers it (the `NotOwned` branch in `ResolveBoardSpaceForPlayer` is
still a TODO), and `PropertyService.NormaliseRentLevels` (called at settlement).

---

## 1. Purpose & Scope

An auction transfers an unwanted/unaffordable property to the highest bidder
at the table, per `game-rules.md` Default rule 6. It is **app-mediated**: the
engine runs a structured bid loop (one `AuctionBidPrompt` per bidder at a
time), enforces the rules on every bid, and settles the winner — rather than
letting the table negotiate verbally and recording only the result.

**In scope:** the trigger, the minimum bid, who bids and in what order, the
bid loop, winner settlement.

**Out of scope:** the reservation mechanic (auctions never collide with it —
see §2), card-driven auction effects (§9), and the bid UI/increment-button
rendering (a frontend concern; the engine only supplies `AllowedIncrements`).

---

## 2. When an Auction Starts — and When It Doesn't

An auction begins when the player who landed on an **unowned, purchasable**
property does not take it:

1. They **decline** the `AcquirePropertyPrompt` (`Accept = false`), or
2. They **cannot afford** it — the engine shows an `AcknowledgePrompt`
   ("You cannot afford this — an auction will begin") and proceeds.

Either path calls into the auction — `AuctionService.RunAuction` — invoked from
`BoardService`'s landed-space resolution (the trigger itself is not yet wired).

**Reservable properties never reach the auction path.** While the reserve
rule is active (`GameModel.ReserveRuleActive`), landing on the final property
of a set the player otherwise owns is a **reserve-or-ignore** decision, not a
buy-or-auction one (`game-rules.md` Reserved Properties). If the player
ignores it, the landing is simply a no-op — there is no auction. This keeps
the auction and reservation systems fully separate; the engine branches to one
or the other *before* an auction could start.

---

## 3. The Minimum Bid (the 50% floor)

An auction does **not** start at £0. It starts at a floor equal to the
property's **reserve price — 50% of its purchase cost, rounded to the game's
rounding grid** (`MoneyHelper.MinAuctionBid`, which is aliased to
`ReservePrice`). The auction's `CurrentHighBid` is initialised to this floor.

The floor is the price the eventual winner pays if nobody raises (§7). Because
it is grid-rounded, the floor plus any grid-aligned increment (§6) stays on
the grid.

---

## 4. Eligibility & Ordering

Bidders are the game's **active players, in clockwise turn order starting from
(and including) the lander**, filtered to those who can afford the floor:

```
eligible = Game.GetPlayers(excludePovPlayer: false)      // active, clockwise from the lander
                .Where(p => p.Money >= floor)
                .ToList();
```

1. **The lander is included.** They declined the *full-price* buy, but
   `game-rules.md` Default rule 6 lets them bid — and they can win at the
   floor. (See the forced-win quirk in §7.)
2. **Players in jail are included** (Default rule 6).
3. **Affordability is filtered up front.** Anyone whose balance is below the
   floor is dropped from the auction immediately — they can never legally bid
   even the minimum, so they are not prompted. This is the key simplification:
   everyone left in the auction can afford the floor, so the winner can always
   pay (§8).
4. **First to act** is the first entry in the filtered list — the lander if
   they cleared the filter, otherwise the next affordable player clockwise.
5. **Empty list → no auction.** If nobody (not even the lander) can afford the
   floor, the auction is cancelled and the landing becomes a **no-op** — the
   property stays bank-owned. Very rare, but well-defined.

---

## 5. The Bid Loop — pass = out

The engine rotates clockwise through the eligible bidders, **skipping whoever
currently holds the high bid** (no bidding against yourself), opening one
`AuctionBidPrompt` at a time:

```
CurrentHighBid       = floor
CurrentHighBidder    = null
loop over eligible bidders (clockwise, skipping CurrentHighBidder):
    AuctionBidPrompt(bidder, BoardIndex, CurrentHighBid, CurrentHighBidder, bidder.Money, AllowedIncrements)
        Bid  → CurrentHighBid = response.BidAmount; CurrentHighBidder = bidder   (stays in)
        Pass → remove bidder from the auction                                    (OUT — for good)
until only the high bidder remains → they win (§7)
```

- **Pass is elimination.** A bidder who passes is out for the rest of *this*
  auction; they are not prompted again. This gives a crisp, deterministic
  termination ("last bidder standing wins") with no round/lap bookkeeping.
- **A bid must strictly exceed `CurrentHighBid`** and not exceed the bidder's
  balance (`game-rules.md` Default rule 7 — bids come from genuine cash; no
  mortgaging, selling, or dealing to fund a bid). The validator
  (`PromptValidator`) enforces both.
- A bidder who cannot afford the smallest legal raise is offered only **Pass**
  (the client hides unaffordable increment buttons).
- The loop runs on the game's single-writer executor and parks on each prompt;
  see §9.

---

## 6. Increments & Rounding

Bidding is by **raise**, not by typing an absolute figure. The engine supplies
`AuctionBidPrompt.AllowedIncrements` from `MoneyHelper.AuctionIncrements(rule)`:

| Rounding rule | Allowed increments |
|---|---|
| None | 1, 5, 10, 20, 50, 100 |
| To5 | 5, 10, 20, 50, 100 |
| To10 | 10, 20, 50, 100 |
| To20 | 20, 50, 100 |
| To50 | 50, 100 |

The client renders one button per increment (each gated against
`PlayerBalance`) and submits `CurrentHighBid + increment` as `BidAmount`. The
response model stays absolute (`BidAmount` is the new total), so the engine
and validator are unchanged from the simple model.

**Bids are settled exact — not re-rounded.** A bid is a deliberate player
choice built from grid-aligned increments off a grid-aligned floor, so it is
already on the grid; `WinAuction` must not run it back through the rounding
rule (which could push it above the bid or the bidder's cash). Computing the
increments server-side keeps the rounding→increment mapping a single source of
truth shared with `MinAuctionBid`.

---

## 7. Winning & Settlement

The auction resolves when every eligible bidder except the high bidder has
passed. The remaining bidder **wins at `CurrentHighBid`**.

- **Last one standing wins — even without bidding.** If everyone else passes
  before anyone raises, the sole survivor wins at the **floor** (the minimum
  bid). They do not have to have placed a bid; being last *is* the win. This
  includes the lander: declining the full-price buy and then being the last
  survivor means they are **forced to take the property at 50%**. This is an
  intentional quirk of the ruleset, not an edge to guard against.

Settlement:

1. `TransactionService.WinAuction(winner, CurrentHighBid, BoardIndex)` — paid
   to the bank, `allowShortfall: false` (it never shortfalls — see §8).
2. `PropertyModel.OwnProperty(winner)`. If that completed a colour set for the
   winner while the reserve rule was still active, the rule is switched off
   (`GameModel.CheckReservationRuleSetObtained`) — winning a set-completing
   property at auction is one of the ways a player "breaks through" to a full
   set, which ends the reservation mechanic for everyone (`game-rules.md`
   Reserved Properties). Then `PropertyService.NormaliseRentLevels`.
3. An `AcknowledgePrompt` to the winner.

**The winning bid *is* the price.** No station price-scaling and no other
outright-purchase cost rules apply to an auction — those only govern buying a
property outright (`game-rules.md` Stations rule 3 already exempts
non-purchase acquisitions). The winner pays exactly the bid.

---

## 8. Why There's No Shortfall or Bankruptcy

Auctions can **never** open a `ShortfallPrompt` or bankrupt a player, by
construction:

- Every eligible bidder was pre-filtered to afford the floor (§4).
- Every raise was capped at the bidder's balance by the validator (§5).
- Therefore the winner — whether they won at the floor (affordable by the
  filter) or at a raise (affordable by the cap) — can always pay in full.

The only "can't afford" outcome is the empty-eligible case (§4.5), which
cancels the auction before it starts. So there is no bank-forfeit or
debt-settlement branch at settlement at all — a deliberate consequence of
filtering affordability up front rather than at the end.

---

## 9. Where It Lives & How It Runs

- **`AuctionService`** (engine sub-system) owns `RunAuction` and the loop. It is
  invoked from `BoardService`'s landed-space resolution (the `NotOwned` branch —
  not yet wired), so an auction is **mid-execution** — part of resolving a
  landing, not a player-initiated command.
- It runs on the game's **single-writer executor pump** (`web-orchestration.md`
  §2). The loop `await`s each `AuctionBidPrompt`, parking the pump until that
  bidder responds; the next prompt opens only after the previous resolves, so
  bids are strictly sequential and the cache has one writer throughout.
- Bids arrive via `GamePlayHub.SubmitPrompt` (out-of-band — it resolves the
  parked prompt's awaiter directly). Host-bypass applies: the host can submit
  any bidder's bid on the tablet.
- Each opened prompt broadcasts the current high bid to the group (the prompt
  seam + the whole-cache `StateChanged` frame), so every device sees the
  auction state live.

---

## 10. Cards Seam (deferred)

Per the cards-are-a-mini-engine decision, no card behaviour is built into the
auction. A card could later force an auction, alter the floor, or override the
outcome; the auction exposes the natural seams (the trigger, the floor, the
winner/price) for the card engine to hook into when it is built. Until then,
auctions run purely on the rules above.

---

## 11. Open / TODO

1. **`game-rules.md` has no Auctions section.** The 50% floor (= reserve
   price), grid-increment bidding, pass-out elimination, the forced
   last-survivor win, the affordability filter, and the empty→no-op rule are
   all new and not yet in the behavioural contract. Add a section there so the
   doc / engine / tests move together (`game-engine.md` §6).
2. **`AuctionService` + the `BoardService` trigger** are unbuilt — the
   `NotOwned` landing branch is a TODO.
3. **`PropertyService.NormaliseRentLevels`** (called at settlement) is itself
   still pending — carried from the property-primitive work.
4. **Starting bidder confirmed** as the lander (filtered); revisit only if the
   "decliner bids last" alternative is ever wanted.
5. **Tests** (once `AuctionService` lands): the affordability filter and
   empty→no-op, pass-out termination, last-survivor-wins-at-floor (including
   the forced lander), bid validation (> high, ≤ balance), and the
   no-shortfall guarantee.

---

## 12. Traceability

1. **`game-rules.md`** — Default rule 6 (declined/unaffordable → auction, who
   may bid), Default rule 7 (bids from genuine cash), Reserved Properties (the
   reserve-or-ignore branch that bypasses auctions), Stations rule 3 (no
   scaling on non-purchase acquisition). *(Auctions section pending — §11.1.)*
2. **`choice-events.md` §15.8** — the `AuctionBidPrompt` contract (fields,
   response, authorisation).
3. **`transactions.md`** — `WinAuction` (the money move) and the no-shortfall
   reasoning.
4. **`turn-state.md` / `web-orchestration.md`** — the executor the loop runs
   on; auctions are mid-execution, not a command.
5. **Code** — `MP.GameEngine/Helpers/MoneyHelper.cs`
   (`MinAuctionBid` / `ReservePrice` / `AuctionIncrements`),
   `Models/Prompts/PromptTypes/AuctionBidPrompt.cs`,
   `Services/SubSystems/BoardService.cs` (trigger),
   `Services/TransactionService.cs` (`WinAuction`).
