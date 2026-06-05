# Transactions — Money Movement & Shortfall

The single seam every money movement in the engine flows through. Pairs with
the event-receipt stream (`event-receipts.md`) — receipts are the *narrative*,
this service is the *mechanism*.

**Status:** built and live. Public surface complete and driven by real
callers — the turn orchestrator and the `Property` / `Jail` / `Go` / `Player`
sub-services route every money movement through here. The five shortfall
sub-services it dispatches to (Loan, Mortgage, sell-building, settling-deal,
Bankruptcy) are still TODO-stubbed inside `ResolveShortfall`.

---

## 1. Purpose & Scope

`TransactionService` is the only place in the engine that mutates
`PlayerModel.Money` or `GameModel.FreeParkingAmount`. Every money movement
goes through here; every `FinancialTransactionReceipt` is emitted here.

**In scope:**

- Player balance changes.
- Free Parking pot changes.
- Shortfall handling (opening `ShortfallPrompt`, dispatching to sub-services).
- Emitting `FinancialTransactionReceipt` from both perspectives.

**Out of scope** — the caller's job after a successful call:

- Property ownership and state changes (`PropertyModel.OwnProperty`,
  `MortgageProperty`, etc.) and the `PropertyTransferReceipt` that
  accompanies them.
- Adding a `LoanModel` to `player.Loans` after `TakeLoan`.
- Building-level changes (`PropertyModel.RentLevel`).
- Card draws, prompt opening for anything other than shortfall.

If you find yourself writing `player.Money += X` outside this service,
something is wrong.

---

## 2. One method per `FinancialReason`

Every value of `FinancialReason` has exactly one public method. Method names
encode the direction so call sites read as English and never have to think
about signs:

- `Pay…` — debit (player pays out).
- `Receive…` / `Take…` — credit (player receives).
- `Process…` — direction is data-driven (currently only
  `ProcessDealPayment`).

Adding a reason = adding a method, not threading a discriminator through.

**`Fine` is not a reason.** All fine-shaped payments map to existing
reasons: card-driven fines use `CardCharge`, the jail-leave fee is
`JailFee`, tax-space payments are `Tax`. There is no movement that needs a
separate `Fine` axis to group by.

---

## 3. Sign convention

Mirrors `FinancialTransactionReceipt.Amount` (`event-receipts.md` §4):
**signed from the subject player's perspective**. Positive = received,
negative = paid.

Internally, the core `Move` helper takes one signed `long amount` and
applies it directly:

```csharp
player.Money = (uint)(player.Money + amount);
if (counterpartyPlayer is not null)
    counterpartyPlayer.Money = (uint)(counterpartyPlayer.Money - amount);
```

One formula. The mirror sign on the counterparty player and the
Free Parking pot fall out of this — no role-swap, no per-branch sign
logic, no inverted special cases.

---

## 4. Quirk: the subject is always the payer

The big one. When the caller has a choice of perspective — *who* is the
`player` argument and *who* is the `counterpartyPlayer`? — they must call
from the **payer's POV**, never the receiver's.

### Why

`Move` only runs the affordability check and opens `ShortfallPrompt` for
the subject (the `player` parameter). The counterparty's balance is
mutated directly with no checks. Therefore the subject must be the player
who could fail to pay.

If you called "Happy Birthday — everyone pays you £25" from the receiver's
POV (`player = birthdayPlayer`, `counterpartyPlayer = payer`,
`amount = +25`), each payer's balance would silently go negative
(uint underflow) and no shortfall prompt would open. Wrong.

### Pattern: caller loops over payers

```csharp
foreach (var payer in engine.Cache.Game.GetActivePlayers(excludePovPlayer: true))
{
    await _transactions.PayCardCharge(
        engine,
        player: payer,                                  // ← subject = PAYER
        amount: 25,
        counterparty: TransactionCounterparty.Player,
        counterpartyPlayer: birthdayPlayer,             // receiver as counterparty
        ct: ct);
}
```

What this buys:

- Each payer's affordability is checked independently. Payer A pays
  outright; payer B takes a loan; payer C declares bankruptcy. Each runs
  its own shortfall resolution.
- Two receipts per call, mirrored: payer perspective (`-25`) +
  receiver perspective (`+25`). The birthday player's receipt stream
  is N positive entries — exactly the narrative ("Alice paid you £25,
  Bob paid you £25…").
- A bankrupt payer doesn't break the others. The receiver ends up short
  by exactly the bankrupt amount — which is the correct game state.
  Whether the bank covers it is a `BankruptcyService` decision (per
  `game-rules.md` Bankruptcy rule 3, only rent and fine debts get the
  bank-covered guarantee).

### `ReceiveCardPayout` is for Bank / Free Parking only

Card credits whose source is the Bank or the FP pot have no payer who
could shortfall — `ReceiveCardPayout` is the natural call there. Using it
with `counterparty: Player` is the trap; that path must go through
`PayCardCharge` from the payer's POV. (See §9 — tighten the validation.)

---

## 5. Quirk: `allowShortfall` is a caller-declared policy

Each `Move` call states whether shortfall is allowed:

| Allowed | Disallowed |
|---|---|
| Rent, Tax, JailFee, FreeParkingPay, MortgageFee, LoanRepay, CardCharge | Purchase, Auction, Build, Unmortgage |
| Debts the player must settle — shortfall opens the prompt | Discretionary spending the caller must pre-gate |

The disallowed cases reflect `game-rules.md` Default rule 7: "Buying a
property and bidding in an auction must be paid from money the player
genuinely has. A player cannot mortgage properties, sell buildings, or
trade with another player to raise funds for either." Extended here to
Build and Unmortgage — neither is a debt; both are discretionary.

If the caller fails to pre-gate one of the disallowed cases and the call
arrives with insufficient funds, `Move` silently no-ops. Per
the engine's policy ([[feedback_engine_error_bubbling]]): mutations
against the rules don't throw — they refuse.

For credits (`amount > 0`), `allowShortfall` is meaningless — the
shortfall branch never fires — so the parameter defaults to false and
callers don't set it.

---

## 6. Quirk: shortfall outcome is tri-state

`ResolveShortfall` returns one of three:

| Outcome | Meaning | Outer transaction |
|---|---|---|
| `FundsRaised` | Loan / mortgage / sell-building gave the player the cash | **continues** — original transaction applies |
| `DebtSettled` | A creditor-deal discharged the debt itself | **stops** — original transaction does NOT apply |
| `Bankrupted` | Player declared bankruptcy | **stops** — original transaction does NOT apply |

The `DebtSettled` case is why a boolean can't represent this. Per
`game-rules.md` Default rule 7: "A debt owed to another player may
instead be settled by a direct deal with that creditor — the deal itself
discharges the debt." If a player chooses `ProposeDeal` on a rent
shortfall and the creditor accepts, the rent is no longer owed. Applying
the original `PayRent` on top would double-charge them and emit a
misleading `Rent` receipt on the stream.

The settling-deal path emits its own `Deal` receipts inside
`DealService`. It must not call back through `TransactionService` to "pay
rent via the deal" — the deal **is** the settlement.

---

## 7. Quirk: `Move` never commits — the turn boundary does

`Move` mutates **only the cache working copy** (`_working`) and returns. It
does **not** call `engine.Cache.SaveChanges()`. Promotion of the working
copy into committed state (`_working` → `_game`) happens in exactly one
place: a turn-state change, via `GameCacheModel.SetTurnState` (and the
snapshot write-back at `TransitionToNextPlayer` / `TransitionToExtraTurn`).
A whole turn's money movements therefore accumulate on one `_working`
instance and commit once, at the boundary.

> **Reversed 2026-06-02.** This section originally described a
> `SaveChanges()` at the end of every `Move`. That was removed: committing
> mid-`Move` promotes `_working` to `_game` and **nulls `_working`**, which
> detaches the `PlayerModel` / `PropertyModel` references the orchestrator
> (and any later `Move` in the same turn) still holds — their subsequent
> mutations then land on an orphaned copy and are silently dropped on the
> next commit. Three bugs traced to this shape before the rule was made
> structural. The code keeps the dead `SaveChanges()` call commented at the
> foot of `Move` with this reasoning, so it isn't re-added.

The rule, stated plainly: **a turn's mutations are committed exactly once,
at the turn-state boundary; rule services must never call `SaveChanges`
mid-turn.** See [[feedback_engine_error_bubbling]] and `turn-state.md` §7.

Recovery is unchanged and still snapshot-based: any exception bubbles to the
web layer, which evicts the cache and re-hydrates from the last snapshot
(the recovery boundary) — never an in-memory per-mutation rollback. Because
nothing commits until the boundary, an uncommitted turn simply vanishes on a
fault, leaving the previous snapshot intact.

---

## 8. Quirk: the Bank isn't modelled

Two non-player counterparties exist (`TransactionCounterparty`):

- **`Bank`** — infinite money, not tracked. A debit to or credit from the
  Bank doesn't adjust anything; the `Counterparty = Bank` on the receipt
  is the only record.
- **`FreeParking`** — finite pot on `GameModel.FreeParkingAmount`. Every
  transaction touching FP adjusts it via the same signed algebra as a
  counterparty player (`FreeParkingAmount - amount`).

If you ever need to know "how much has the bank disbursed?", scan the
receipt stream — there is no balance to query.

---

## 9. Open / TODO

1. **Shortfall sub-services.** Five TODOs in `ResolveShortfall` (Loan,
   Mortgage, sell-building, settling-deal, Bankruptcy). The outcome
   contract is stable; only the implementations are missing.
2. **Tighten `ReceiveCardPayout`** to reject `counterparty: Player` —
   that path must go through `PayCardCharge` from the payer's POV (§4).
   Currently permissive, inviting the trap.
3. **Bankruptcy-via-rent-or-fine compensation.** `game-rules.md`
   Bankruptcy rule 3: a player bankrupted by rent or a fine is paid by
   the bank in full. `ShortfallOutcome.Bankrupted` currently just stops
   the transaction — the recipient gets nothing. `BankruptcyService`
   will need to know the original creditor + reason to wire this up;
   `ResolveShortfall` may need to pass the `FinancialReason` through.
4. ~~**Tests.**~~ Done — `MP.GameEngine.Tests/ServiceTests/TransactionService_Tests.cs`
   covers every public transaction type (debits, credits, player-to-player
   mirroring, the Free Parking pot algebra, board-driven rent, rounding, the
   shortfall flow, and the no-op rules) against a stub `IPromptProvider`.

---

## 10. Traceability

- **`game-rules.md`** — the rules driving when each transaction happens
  (Default rules 5–8, Loans, Mortgaging, Bankruptcy).
- **`event-receipts.md`** — receipt shapes; §4 covers
  `FinancialTransactionReceipt` and the sign convention.
- **`choice-events.md`** — §15.7 covers `ShortfallPrompt`, the prompt
  this service opens on shortfall.
- **`game-engine.md`** — surrounding engine architecture.
- **`MP.GameEngine/Services/TransactionService.cs`** — the implementation.
- **`MP.GameEngine/Enums/FinancialReason.cs`** — the categorical axis;
  one public method per value.
- **`MP.GameEngine/Enums/TransactionCounterparty.cs`** — Bank /
  FreeParking / Player.