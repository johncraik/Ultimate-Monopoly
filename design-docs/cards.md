# All Cards in the Game
All the cards in the game grouped by their type. Each card carries its **action metadata** (the
`MoneyAction` / `MovementAction` / `JailAction` model fields + card-level `ConditionType`/`Trigger`)
and a **build** line — ✅ buildable now · 🟢 functionally complete (action/effect built; only the
held-card trigger / play-card layer pending) · 🟡 primitive exists, flag missing · ⬜ needs a new
action/flag. The build deltas are catalogued in `cards-dev-changes.md` (the `§` refs).

> **"Anytime on your own turn" cards** carry `Trigger=OnTurnStart | OnSpaceLand` — the two play
> windows a held card can be offered on the holder's turn: `OnTurnStart` (before the roll, fired by
> the play-a-card command in `PlayerProfileService`) and `OnSpaceLand` (after any landing — the
> holder's own move or being moved by another's third die — fired by the engine). `CardTrigger.None`
> is reserved for resolve-on-draw cards (`ConditionType=None`), which carry no trigger.

## Chance
You get a chance card from landing on a chance space. These are standard monopoly cards.

**16 cards**: (all resolve-on-draw, `ConditionType=None`, unless noted)
- "Drunk in charge" fine £20
  - `MoneyAction{Direction=Pay, Amount=20, Counterparty=FreeParking}`.
  - ✅ Buildable now.
- Advance to "GO"
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=0}` (GO bonus via the default landing).
  - ✅ Buildable now.
- Advance to Mayfair
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=39}`.
  - ✅ Buildable now.
- Advance to Pall Mall
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=11}`.
  - ✅ Buildable now.
- Advance to Trafalgar Square
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=24}`.
  - ✅ Buildable now.
- Bank pays you dividend of £50
  - `MoneyAction{Receive, 50, Bank}`.
  - ✅ Buildable now.
- Get out of jail free. Keep until needed
  - `JailAction{Kind=Release, Target=Self}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnInJail` (held; played on own turn while in jail).
  - ✅ Buildable now (the built leave-jail-by-card path).
- Go back three spaces
  - `MovementAction{Kind=MoveSpaces, Spaces=-3}`.
  - ✅ Buildable now.
- Go to jail. Move directly to jail. Do NOT pass "GO". Do NOT collect £200
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=100}` (jail; no unique Jail action needed — §4).
  - ✅ Buildable now.
- Make general repairs on all of your buildings. For each house pay £25. For each hotel pay £100.
  - One group, two actions: `MoneyAction{Pay, 25, FreeParking, PerUnit=PerHouse}` + `MoneyAction{Pay, 100, FreeParking, PerUnit=PerHotel}`.
  - ✅ Buildable now (`PerUnit` exists).
- Pay school fees of £150
  - `MoneyAction{Pay, 150, FreeParking}`.
  - ✅ Buildable now.
- Speeding fine £15
  - `MoneyAction{Pay, 15, FreeParking}`.
  - ✅ Buildable now.
- Take a trip to Marylebone Station.
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=15}`.
  - ✅ Buildable now.
- You are assessed for street repairs. £40 per house. £115 per hotel.
  - `MoneyAction{Pay, 40, FreeParking, PerUnit=PerHouse}` + `MoneyAction{Pay, 115, FreeParking, PerUnit=PerHotel}`.
  - ✅ Buildable now.
- Your building loan matures. Receive £150
  - `MoneyAction{Receive, 150, Bank}`.
  - ✅ Buildable now.
- You have won a crossword competition. Collect £100
  - `MoneyAction{Receive, 100, Bank}`.
  - ✅ Buildable now.


## Community Chest
You get a community chest card from landing on a community chest space. These are standard monopoly cards.

**16 cards**: (all resolve-on-draw, `ConditionType=None`, unless noted)
- Advance to "GO"
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=0}`.
  - ✅ Buildable now.
- Annuity Matures. Collect £100
  - `MoneyAction{Receive, 100, Bank}`.
  - ✅ Buildable now.
- Bank error in your favour. Collect £200
  - `MoneyAction{Receive, 200, Bank}`.
  - ✅ Buildable now.
- Doctors fee pay £50
  - `MoneyAction{Pay, 50, FreeParking}`.
  - ✅ Buildable now.
- From sale of stock you get £50
  - `MoneyAction{Receive, 50, Bank}`.
  - ✅ Buildable now.
- Get out of jail free. Keep until needed
  - `JailAction{Kind=Release, Target=Self}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnInJail`.
  - ✅ Buildable now (leave-jail-by-card).
- Go back 3 spaces
  - `MovementAction{Kind=MoveSpaces, Spaces=-3}`.
  - ✅ Buildable now.
- Go back to Old Kent Road
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=1}`.
  - ✅ Buildable now.
- Go to jail. Move directly to jail. Do NOT pass "GO". Do NOT collect £200
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=100}` (no unique Jail action — §4).
  - ✅ Buildable now.
- Income tax refund. Collect £50
  - `MoneyAction{Receive, 50, Bank}`.
  - ✅ Buildable now.
- It is your birthday. Collect £10 from every player
  - `MoneyAction{Receive, 10, Counterparty=EachPlayer}`.
  - ✅ Buildable now.
- Pay a £10 fine or take a "Chance"
  - 2 groups (OR): [`MoneyAction{Pay, 10, FreeParking}`] OR [`DeckDrawAction{Deck=Chance}`]. `CardOptionPrompt`.
  - ✅ Built — `DeckDrawActionService` recurses `CardService.DrawCard` (no DI cycle — reached via `engine.CardService`).
- Pay hospital £100
  - `MoneyAction{Pay, 100, FreeParking}`.
  - ✅ Buildable now.
- Pay your insurance premium £50
  - `MoneyAction{Pay, 50, FreeParking}`.
  - ✅ Buildable now.
- You have won second prize in a beauty contest. Collect £10
  - `MoneyAction{Receive, 10, Bank}`.
  - ✅ Buildable now.
- You inherit £100
  - `MoneyAction{Receive, 100, Bank}`.
  - ✅ Buildable now.


## % Chance
You get a % chance card when landing on a chance space going anti-clockwise. 
The values on these cards are 10/50/100% depending on the player's %cap. "Collect £200" from GO is NOT capped; that is the default function of GO.

**16 cards**: (resolve-on-draw, `ConditionType=None`; `PercentageApplies=true` → `Amount` is the 100% value, realised 10/50/100 by the holder's %cap)
- Advance 8 spaces
  - `MovementAction{Kind=MoveSpaces, Spaces=8}`.
  - ✅ Buildable now.
- Advance to Bond Street
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=34}`.
  - ✅ Buildable now.
- Advance to Euston Road
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=8}`.
  - ✅ Buildable now.
- Advance to "GO". Collect £200, and roll 2 dice * £100
  - One group: `MovementAction{AdvanceToIndex=0}` (the £200 is the default GO landing — uncapped) + `MoneyAction{Receive, Amount=100, Bank, DiceMultiplier=TwoDice, PercentageApplies=true}`.
  - ✅ Buildable now (`DiceMultiplier=TwoDice` + `PercentageApplies`).
- Get out of jail free. Keep until needed
  - `JailAction{Kind=Release, Target=Self}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnInJail`.
  - ✅ Buildable now.
- Go back to Bow Street
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=16}`.
  - ✅ Buildable now.
- Make general repairs on all your houses. For each house pay £100. For each hotel pay £400
  - `MoneyAction{Pay, 100, FreeParking, PerUnit=PerHouse, PercentageApplies=true}` + `MoneyAction{Pay, 400, FreeParking, PerUnit=PerHotel, PercentageApplies=true}`.
  - ✅ Buildable now.
- Pay each player £100 and go to jail
  - `MoneyAction{Pay, 100, Counterparty=EachPlayer, PercentageApplies=true}` + `MovementAction{AdvanceToIndex=100}`.
  - ✅ Buildable now.
- Pay university fees of £1000
  - `MoneyAction{Pay, 1000, FreeParking, PercentageApplies=true}`.
  - ✅ Buildable now.
- Speeding fine. Pay £300
  - `MoneyAction{Pay, 300, FreeParking, PercentageApplies=true}`.
  - ✅ Buildable now.
- You are assessed for street repairs. £160 per house. £460 per hotel
  - `MoneyAction{Pay, 160, FreeParking, PerUnit=PerHouse, PercentageApplies=true}` + `MoneyAction{Pay, 460, FreeParking, PerUnit=PerHotel, PercentageApplies=true}`.
  - ✅ Buildable now.
- You fail a breathalyser test. Pay £500
  - `MoneyAction{Pay, 500, FreeParking, PercentageApplies=true}`.
  - ✅ Buildable now.
- You have been robbed. Pay £1000 to the player who rolls the highest on one die
  - `MoneyAction{Pay, 1000, Counterparty=DiceOffPlayer, DiceOff={Highest=true}, PercentageApplies=true}`.
  - ✅ Built — dice-off via the generic `DiceOffPlayer` counterparty + `DiceOff` config (highest one-die roller; pool excludes the holder); resolved through `DiceService.ResolveDiceOffTarget`/`RollDiceOff`.
- You have sold your car. Collect £2000
  - `MoneyAction{Receive, 2000, Bank, PercentageApplies=true}`.
  - ✅ Buildable now.
- You win the lottery. Collect £3000
  - `MoneyAction{Receive, 3000, Bank, PercentageApplies=true}`.
  - ✅ Buildable now.
- Your book sales ear you £1500
  - `MoneyAction{Receive, 1500, Bank, PercentageApplies=true}`.
  - ✅ Buildable now.


## % Community Chest
You get a % community chest card when landing on a community chest space going anti-clockwise.
The values on these cards are 10/50/100% depending on the player's %cap. "Collect £200" from GO is NOT capped; that is the default function of GO.

**16 cards**: (resolve-on-draw, `ConditionType=None`; `PercentageApplies=true` → `Amount` = the 100% value)
- Advance to "GO". Collect £200. Each player (not including you) collects two die * £100
  - `MovementAction{AdvanceToIndex=0}` (the £200 default GO) + a £100 × 2-dice grant **from the bank to every other player**, %-capped per the *receiving* player.
  - ✅ Built — `MovementAction{AdvanceToIndex=0}` + `MoneyAction{Receive, 100, Bank, Target=AllOthers, DiceMultiplier=TwoDice, PercentageApplies=true}` (cap follows each receiving subject).
- Advance to the nearest station owned by someone else
  - `MovementAction{Kind=AdvanceToNearest, Nearest=Station}` + an "owned by another" filter.
  - ✅ Built — `MovementAction{Kind=AdvanceToNearest, Nearest=Station, NearestOwnedByOther=true}`. No station owned by another → no move.
- Bank interest. Receive £500
  - `MoneyAction{Receive, 500, Bank, PercentageApplies=true}`.
  - ✅ Buildable now.
- Burst tyre. Pay £200
  - `MoneyAction{Pay, 200, FreeParking, PercentageApplies=true}`.
  - ✅ Buildable now.
- From the sale of shares you receive £1000
  - `MoneyAction{Receive, 1000, Bank, PercentageApplies=true}`.
  - ✅ Buildable now.
- Get out of jail free. Keep until needed
  - `JailAction{Kind=Release, Target=Self}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnInJail`.
  - ✅ Buildable now.
- Go back 11 spaces
  - `MovementAction{Kind=MoveSpaces, Spaces=-11}`.
  - ✅ Buildable now.
- Go back to income tax
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=4}`.
  - ✅ Buildable now.
- Happy New Year! Collect £300 from each player
  - `MoneyAction{Receive, 300, Counterparty=EachPlayer, PercentageApplies=true}` (holder-vs-each; cap by the holder = default).
  - ✅ Buildable now.
- Insurance claim successful. Collect £300
  - `MoneyAction{Receive, 300, Bank, PercentageApplies=true}`.
  - ✅ Buildable now.
- Mass breakout from prison. All prisoners escape to just visiting
  - `MovementAction{Kind=GoToJustVisiting}` applied to **every player in jail** (index 100 → 10, counter direction of travel). A Movement action (§6 resolved).
  - ✅ Built — `MovementAction{Kind=GoToJustVisiting, Target=Everyone, JailFilter=OnlyJailed}` (counter-direction already handled by the `GoToJustVisiting` case).
- Pay a £400 fine or take a "Percentage Chance"
  - 2 groups (OR): [`MoneyAction{Pay, 400, FreeParking, PercentageApplies=true}`] OR [`DeckDrawAction{Deck=PercentageChance}`]. `CardOptionPrompt`.
  - ✅ Built — `DeckDrawActionService` recurses `CardService.DrawCard`.
- Swap places with any player of your choice
  - `MovementAction{Kind=Swap, Target=ChosenPlayer}`.
  - ✅ Buildable now (`Swap` exists; `ResolveLandedSpace=false`).
- You inherit £1000
  - `MoneyAction{Receive, 1000, Bank, PercentageApplies=true}`.
  - ✅ Buildable now.
- You make a donation to charity. Pay £200 * the number of properties you own
  - `MoneyAction{Pay, 200, FreeParking, PerUnit=PerProperty, PercentageApplies=true}`.
  - ✅ Buildable now (`PerUnit=PerProperty`).
- You steal £200 from each player but go to jail
  - `MoneyAction{Receive, 200, Counterparty=EachPlayer, PercentageApplies=true}` + `MovementAction{AdvanceToIndex=100}`.
  - ✅ Buildable now.

## Third
You get a third card from landing on chance or community chest space going anti-clockwise.
You also get a third card from you or anyone else rolling your dice number.

> Metadata key: each card lists its **group(s)/action(s)** with the real model fields
> (`MoneyAction` / `MovementAction` / `JailAction` + the card-level `ConditionType` / `Trigger`),
> then a **build** line — ✅ buildable now · 🟡 partial (primitive exists, flag missing) · ⬜ needs
> a new action/flag. `§` refs point at `cards-dev-changes.md`.

**35 cards**:
- A player of your choice must accompany you to jail
  - One group, two actions: self → jail (`MovementAction{AdvanceToIndex=Jail}` per the "go-to-jail = movement" decision) + `JailAction{Kind=SendToJail, Target=ChosenPlayer}`. `ConditionType=None`.
  - ✅ Buildable now (`JailAction` + `ChosenPlayer` via TargetPlayer prompt).
- Advance 1 space. Keep until needed
  - `MovementAction{Kind=MoveSpaces, Spaces=1}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart | OnSpaceLand` (anytime, own turn). Card `SuppressDefault{SuppressBoardResolution}` — the card resolves its own destination (`ResolveLandedSpace=true`), so the original landing must not re-resolve.
  - ✅ Buildable now.
- Advance 3 spaces. Keep until needed
  - `MovementAction{Kind=MoveSpaces, Spaces=3}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart | OnSpaceLand`. Card `SuppressDefault{SuppressBoardResolution}`.
  - ✅ Buildable now.
- Advance 5 spaces.
  - `MovementAction{Kind=MoveSpaces, Spaces=5}`. `ConditionType=None` (resolve-on-draw, not kept).
  - ✅ Buildable now.
- Advance up to 5 spaces. Keep until needed
  - 5 groups, each `MovementAction{Kind=MoveSpaces, Spaces=1..5}`; `CardOptionPrompt` picks. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart | OnSpaceLand`. Card `SuppressDefault{SuppressBoardResolution}`.
  - ✅ Buildable now ("up to N" = N groups, §4 — no quantity prompt).
- All outstanding loans are wiped out for all players. Any player with no loans receives £1000 but must return a property to the bank
  - Compound, deterministic: snapshot who holds an outstanding loan → **Loans** wipe (all players) → `MoneyAction{Receive, £1000, Bank}` to the snapshot's no-loan players → **Property** return-to-bank (their chosen property) from those same players.
  - ✅ Built — `LoansAction{Kind=WipeAllAndRewardClear}`: snapshots loan-free players → wipes everyone's loans → £1000 + forced property-return to the clear players (reuses `PropertyActionService`; £1000 kept even with no property to give back).
- All players immediately go to jail
  - `JailAction{Kind=SendToJail, Target=Everyone}`. `ConditionType=None`.
  - ✅ Buildable now (`Target=Everyone` added this increment; jail self + all others).
- Cancel a players triple bonus. Keep until needed
  - `DiceAction{Kind=ModifyTripleBonus, PayoutFactor=0, Target=ChosenPlayer}`. `ConditionType=ChoiceAnyPlayerTurn`, `Trigger=OnOtherRollsTriple`.
  - 🟢 Functionally complete — `DiceActionService` zeroes the target's triple-bonus *payout* via `PlayerService.ApplyTripleBonus(target, factor:0)`, while the accumulator still increments +£500 (the payout-vs-accumulator split). Remaining: the held/reactive firing on another player's triple (`OnOtherRollsTriple`) and suppressing that player's default bonus — the trigger layer.
- Change direction. Keep until needed
  - **Direction** action: `PlayerModel.FlipDirection()` (self). `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart` (turn-start only — flipping direction mid-board on a landing isn't offered; you pick direction before rolling).
  - ✅ Built — `DirectionActionService` (action done; held-play funnels through the `PlayCard` seam).
- Convert a double into a triple. Keep until needed
  - `DiceAction{Kind=ConvertDoubleToTriple}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnRollDouble`.
  - 🟢 Functionally complete — `DiceActionService` sets `GameModel.ModifiedDiceRollType=Triple`; the orchestrator re-routes the roll as a triple (triple bonus, combined-total move, no third die) *before* the `Doubles/TriplesInRow` counters update (§4). Remaining: the held firing on the holder's double (`OnRollDouble`) — the trigger layer.
- Each player changes direction, including any in jail
  - **Direction** action: `FlipDirection()` for all players (incl. self), **not** excluding jailed. `ConditionType=None`.
  - ✅ Built — `DirectionActionService` with `Target=Everyone` (jailed are included by default — no extra flag needed).
- Each player receives £1000 but must return a property to the bank
  - Every player (incl. holder): `MoneyAction{Receive, £1000, Bank}` + **Property** return-to-bank (their chosen property).
  - ✅ Built — `MoneyAction{Receive, 1000, Bank, Target=Everyone}` + `PropertyAction{Kind=ReturnToBank, Target=Everyone}`.
- Go back 1 space
  - `MovementAction{Kind=MoveSpaces, Spaces=-1}`. `ConditionType=None`.
  - ✅ Buildable now (signed `Spaces`).
- Go back to free parking
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=FreeParking}`. `ConditionType=None`.
  - ✅ Buildable now.
- Go to jail for 10 turns. You can roll the dice but cannot leave jail. You can collect all rent due. Keep until needed
  - `JailAction{Kind=SendToJail, Target=Self, TurnsOverride=10, MinJailTurns=10, CollectRentInJail=true}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart | OnSpaceLand` (the strategic self-play). Card `SuppressDefault{SuppressBoardResolution}` — the holder moves to jail, so the original landing must not resolve.
  - 🟢 Functionally complete — `JailActionService` applies `MaxJailTurnsOverride`/`MinJailTurns`/`CollectRentInJail` on a successful jailing; `PlayerModel.CanLeaveJail` (the `MinJailTurns` lock) blocks every exit — double, fee, card, and the `TurnStateProvider.CanLeaveJail` command gate — until turn 10, then `ForcePlayerToLeaveJail` releases; `PropertyService` collects rent while jailed; the flags reset on every exit. Remaining: the anytime-own-turn play-from-hand (the play-card layer).
- Hand in any property into free parking. This does not get recorded and can be from a set you have handed in before
  - **Property** action: hand-in to FP (self's chosen property), `not-recorded` (don't append to `PlayerModel.FPHandedInSets`), silent no-op if no valid property. `ConditionType=None`.
  - ✅ Built — `PropertyActionService` (`Kind=HandInToFreeParking`); eligible = `TradableProperties`, silent no-op if none. `PropertyTransferService.HandIntoFreeParking` doesn't touch `FPHandedInSets`, so it's already "not recorded".
- Have an extra 3 turns
  - **Turns** action: `PlayerModel.ExtraTurns += 3` (self). `ConditionType=None`.
  - ✅ Built — `TurnsActionService` (`Kind=ExtraTurns, Turns=3`).
- Miss 3 turns
  - **Turns** action: `PlayerModel.TurnsToMiss += 3` (self). `ConditionType=None`.
  - ✅ Built — `TurnsActionService` (`Kind=MissTurns, Turns=3`).
- Have an extra 3 turns or make the player rolling the lowest on one die miss 3 turns
  - 2 groups (OR): [`TurnsAction{Kind=ExtraTurns, Turns=3, Target=Self}`] OR [`TurnsAction{Kind=MissTurns, Turns=3, DiceOff={Highest=false, IncludeHolder=false}}`]. `CardOptionPrompt`. `ConditionType=None`.
  - ✅ Built — `TurnsActionService` applies the self group's `ExtraTurns += 3`, and routes the `DiceOff` group through the generic `DiceService.ResolveDiceOffTarget`/`RollDiceOff` picker (pool = other players, lowest one-die roller) → that player's `TurnsToMiss += 3`. The dice-off is now a generic non-money consequence picker shared with the money counterparties and triple-bonus redirect.
- Hidden treasure. Collect £1000 from the bank, or £350 from each player
  - 2 groups (OR): [`MoneyAction{Receive, £1000, Bank}`] OR [`MoneyAction{Receive, £350, EachPlayer}`]. `CardOptionPrompt`. `ConditionType=None`.
  - ✅ Buildable now.
- Your next tax payment is tripled
  - Held `MoneyAction{Direction=Pay, Counterparty=FreeParking, AmountSource=TriggerAmount, Amount=3}` (Amount reused as the ×3 factor). `ConditionType=MetCardholderTurn`, `Trigger=OnTaxLanded`, duration once.
  - 🟢 Functionally complete (action) — `AmountSource{Fixed, TriggerAmount}` on `MoneyAction` + an optional `CardActionContext` threaded through `CardService.PlayCard → ResolveCard → ApplyAction →` the handlers; `MoneyActionService.RealiseAmount` resolves `TriggerAmount` as `context.TriggerAmount × Amount` (no context → 0, a silent no-op). The seam is shared by every `AmountSource=TriggerAmount` card. **`OnTaxLanded` added to `CardTrigger` (1<<15); written to `third.json`** (`Money{Pay, FreeParking, AmountSource=TriggerAmount, Amount=3}`, `MetCardholderTurn`, `Trigger=OnTaxLanded`). Remaining (trigger layer): the held firing supplying the assessed tax as context + duration "next" (§3.6).
- Pay each player £200
  - `MoneyAction{Direction=Pay, Amount=200, Counterparty=EachPlayer}`. `ConditionType=None`.
  - ✅ Buildable now.
- Pay each player £300 the next time you land on GO
  - Held `MoneyAction{Pay, £300, EachPlayer}`. `ConditionType=MetCardholderTurn`, `Trigger=OnLandGo`, duration once (`TurnsRemaining=1`).
  - 🟢 Functionally complete — `MoneyAction{Direction=Pay, Amount=300, Counterparty=EachPlayer}` is fully built (each other player paid from the holder, payer-POV). Remaining (trigger layer): the held firing on `OnLandGo` and duration "next time" (once, `TurnsRemaining=1`, §3.6).
- Pay the money you receive for snake eyes to the player who rolls the lowest number
  - Held: redirect the snake-eyes (double-1) £500 bonus → `MoneyAction{Pay, Counterparty=LowestRoller, AmountSource=TriggerAmount}`. `ConditionType=MetCardholderTurn`, `Trigger=OnRollDouble` (refined to snake eyes).
  - ✅ Action built + **`OnSnakeEyes` trigger added (1<<16); written to `third.json`** (`Money{Pay, DiceOffPlayer, Basis=SnakeEyesBonus, DiceOff{}}`, `MetCardholderTurn`, `Trigger=OnSnakeEyes`). The "pay" models the redirect — the holder receives the £500 by default, then pays it to the lowest roller (net correct, no suppress needed). Held firing via the trigger layer.
- Purge 2 of your properties
  - **Building** purge action: purge 2 of the holder's own properties (chosen). `ConditionType=None`.
  - ✅ Built — `BuildingActionService` (`Kind=Purge, Target=Self, Count=2`).
- Purge an opponent's property of your choice
  - **Building** purge: a `ChosenPlayer`'s chosen property. `ConditionType=None`.
  - ✅ Built — `BuildingActionService` (`Kind=Purge, Target=ChosenPlayer, Count=1`); `PurgingService.PurgeOthersProperty` prompts for the player.
- Return a property to the bank
  - **Property** action: title → bank, self's chosen property (TargetProperty). `ConditionType=None`.
  - ✅ Built — `PropertyActionService` (`Kind=ReturnToBank`).
- Receive the money from free parking that another player would have received. Keep until needed
  - Held `MoneyAction{Receive, Counterparty=FreeParking, AmountSource=TriggerAmount, Amount=1}` (Amount=1 → exactly the FP-take amount) + card `SuppressDefault{SuppressFreeParkingMoneyTake}`. `ConditionType=ChoiceAnyPlayerTurn`, `Trigger=OnOtherTakesFreeParking`.
  - ✅ Built — written to `third.json`. The bystander's receive rides the `AmountSource=TriggerAmount` seam (the FP take amount threaded by `OnOtherTakesFreeParking`), and the card-level `SuppressFreeParkingMoneyTake` is consumed by `FreeParkingService.TakeFromFreeParking` (it aggregates the trigger result and gates the taker's money take), so the bystander gets the pot money and the taker takes none. No group-scoped suppress needed after all — the trigger-result consumption at the call site does it.
- Send any player of your choice to jail
  - `JailAction{Kind=SendToJail, Target=ChosenPlayer}`. `ConditionType=None`.
  - ✅ Buildable now.
- Swap all your money with another player
  - Money-swap with a `ChosenPlayer`. `ConditionType=None`.
  - ✅ Built — `MoneyAction{SwapCash=true}` (chosen player; or a dice-off roller via `Counterparty`).
- You receive no cash on your next visit to free parking
  - Held: suppress the holder's FP money take on the next visit. `ConditionType=MetCardholderTurn`, `Trigger=OnLandFreeParking`, duration once.
  - 🟢 Functionally complete — written to `third.json` as a **`NoOpAction`** (the new suppress-only action seam) carrying card-level `SuppressDefault{SuppressFreeParkingMoneyTake}` (honoured by `FreeParkingService.TakeFromFreeParking`), `MetCardholderTurn`/`OnLandFreeParking`. Remaining (trigger layer): the held `OnLandFreeParking` firing applying the suppress + duration-once (§3.6).
- Your fee to leave jail has been tripled
  - `JailAction{Kind=ModifyLeaveFee, LeaveFeeMultiplier=3, Target=Self}` (applies to the holder's next jail exit). `ConditionType=None` — a pure downside, so it resolves on draw, not a held choice.
  - ✅ Built — `JailActionService` `ModifyLeaveFee` multiplies `PlayerModel.JailCost`.
- Your next payment to another player is doubled
  - Held `MoneyAction{Pay, Counterparty=TriggerPlayer, AmountSource=TriggerAmount, Amount=2}` + card `SuppressDefault{SuppressRent}`. `ConditionType=MetCardholderTurn`, `Trigger=OnRentDue`, duration once (single-use). **Replace** (not additive) — the card pays the **multiplied** rent (×2) to the owner itself and suppresses the default rent, so the owner collects 2× via the card and the default rent acknowledge + charge are skipped.
  - ✅ Built — new `MoneyCounterparty.TriggerPlayer` resolves to `CardActionContext.TriggerCounterpartyId`; `PropertyService.PayPropertyRent` threads the owner into `OnPayRent` (→ `OnRentDue`) and, on `SuppressRent` from the trigger, **skips its rent acknowledge + default `PayRent`**. `MoneyActionService.ApplyToTriggerPlayer` pays that owner (payer-POV, `FinancialReason.Rent` on the receipt/notification). Written to `third.json` (`Money{Pay, TriggerPlayer, AmountSource=TriggerAmount, Amount=2}` + `SuppressDefault{SuppressRent}`, `MetCardholderTurn`/`OnRentDue`). Forced single-use — fires once on the next rent, then returns to the deck. (New `SuppressDefault.SuppressRent` flag, bit 4096.)
- Your next triple is downgraded to a double
  - Held **Dice** action: downgrade the holder's triple → double. `ConditionType=MetCardholderTurn`, `Trigger=OnRollTriple`.
  - 🟢 Functionally complete — `DiceActionService` (`DiceKind.DowngradeTripleToDouble`) sets `GameModel.ModifiedDiceRollType=Double`; `PlayerTurnOrchestrator.TripleRoll` re-routes the roll through `HandleDoubleRoll` (two-dice move, direction flip) *before* the `Doubles/TriplesInRow` counters update (§4). Remaining: the held firing on the holder's triple (`OnRollTriple`) — the trigger layer.
- Your money for landing on GO is doubled for the next 5 occasions
  - Held: modify the GO bonus `×2`. `ConditionType=MetCardholderTurn`, `Trigger=OnLandGo`, duration 5 (`TurnsActive=5`/`TurnsRemaining`).
- 🟢 Functionally complete — written to `third.json` (`Money{Receive, Bank, AmountSource=TriggerAmount, Amount=2}` + card `SuppressDefault{SuppressGoBonus}` + group `TurnsActive=5`, `MetCardholderTurn`/`OnLandGo`): pays ×2 the GO bonus and suppresses the default. Remaining (trigger layer): the held `OnLandGo` firing + the duration-5 multi-use consumption (`TurnsRemaining` initialised from `TurnsActive`, §3.6).


## Double
You get a double card for rolling a double (or a triple downgraded to a double)

**10 cards**: (resolve-on-draw, `ConditionType=None`, unless noted. Global events map onto `GameModel.GlobalEventInfo` (`EventInfo`) + `GlobalEvent`; store/read-hooks + clear-on-double are built — only the card-action that *sets* them is new, §3a)
- Advance to the nearest buildable property owned by another player
  - `MovementAction{Kind=AdvanceToNearest, Nearest=ColourProperty, NearestOwnedByOther=true}`. No buildable property owned by another → no move (fallback to current index).
  - ✅ Built — `MovementActionService.FindNearest` (`ColourProperty` → `BuildablePropertyIndexes`) + the `NearestOwnedByOther` filter; same path as the %ComChest nearest-station card.
- Collect £500 and do not turn around
  - `MoneyAction{Receive, 500, Bank}` + card `SuppressDefault{SuppressDirectionChange}` (suppresses the double's `FlipDirection`).
  - ✅ Built — money via `MoneyActionService`; `PlayerTurnOrchestrator:112` gates the double's `FlipDirection` on `!suppressDefault.SuppressDirectionChange` (the SuppressDefault refactor).
- Tax Rise! All taxes are doubled until another double is rolled [GLOBAL EVENT]
  - Set `EventInfo.TaxMultiplier = 2`.
  - ✅ Built — `GlobalEventActionService` (maps the named event via `GlobalEventService`); clear-on-double built.
- FREE Free Parking! Free parking is disabled until another double is rolled [GLOBAL EVENT]
  - Set `EventInfo.RealFreeParking = true`.
  - ✅ Built — `GlobalEventActionService` (maps the named event via `GlobalEventService`); clear-on-double built.
- Prisons at max capacity! No player can go to jail, instead they must pay their jail fee when sent to jail. This happens until another double is rolled [GLOBAL EVENT]
  - Set `EventInfo.JailFull = true` (the card its code comment already quotes).
  - ✅ Built — `GlobalEventActionService` (maps the named event via `GlobalEventService`); clear-on-double built.
- Send any player of your choice to jail. Keep until needed
  - `JailAction{Kind=SendToJail, Target=ChosenPlayer}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart | OnSpaceLand` (anytime, own turn).
  - ✅ Buildable now.
- Receive ALL the money from free parking (uncapped). Keep until needed
  - Held: receive the whole FP pot (`GameModel.FreeParkingAmount`), uncapped. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnLandFreeParking` (play on landing FP).
  - ✅ Action built — `MoneyAction{Receive, Counterparty=FreeParking, Basis=PercentOfFreeParkingPot, Amount=100}`. (Held; fires via the trigger layer.)
- Your double is converted into a triple
  - `DiceAction{Kind=ConvertDoubleToTriple}`, resolve-on-draw. `ConditionType=None`.
  - ✅ Built — `DiceActionService` sets `GameModel.ModifiedDiceRollType=Triple`; `PlayerTurnOrchestrator` double branch re-routes through `ResolveTripleBonus`+`HandleTripleRoll` *before* the `Doubles/TriplesInRow` counters bump at `TransitionToExtraTurn` (§4).
- Swap a set with another player. Both sets get purged
  - `PropertyAction{Kind=SwapSet}`, resolve-on-draw. `ConditionType=None`.
  - ✅ Built — `PropertyActionService.SwapSet`: holder picks one of their complete buildable sets, a target player who holds a complete set, and which of the target's sets to take; every title in each set is exchanged (`PropertyTransferService.Transfer`), then both swapped sets are purged (`PurgingService.PurgeProperties`, attributed to each set's new owner). No-op if the holder — or every opponent — lacks a complete set. Modelled as one action since the purge needs the swapped-set identity (§2.12).
- Your credit rating plummets! Repay all your loans in full
  - **Loans** action: repay all the holder's outstanding loans in full (`GetOutstandingLoans`).
  - ✅ Built — `LoansActionService` (`Kind=RepayAll`; shortfall allowed via `TransactionService.RepayLoan`).


## Triple
You get a triple card for rolling a triple (or a double upgraded to a triple)

**10 cards**: (resolve-on-draw, `ConditionType=None`. "Triple bonus" = `PlayerModel.TripleBonus`, credited by `PlayerService.ResolveTripleBonus`)
- Choose any available property from the bank
  - **Property** action: acquire a chosen bank-owned property (TargetProperty over the bank pool).
  - ✅ Built — `PropertyAction{Kind=TakeFromBank}` (free acquisition via `PropertyTransferService.Buy`).
- Each player rolls one die. The player with the lowest roll receives your triple bonus
  - `DiceAction{Kind=ModifyTripleBonus, PayoutRedirectToLowestRoller=true}` + card `SuppressDefault{SuppressTripleBonus}`. `ConditionType=None`.
  - ✅ Built — `DiceActionService.ModifyTripleBonus` rolls a one-die dice-off (`IncludeHolder=true`, lowest) and credits the holder's bonus to that roller via `PlayerService.ApplyTripleBonus(factor:1)` (accumulator still +£500; holder-wins → keeps it). **Requires** the card's `SuppressTripleBonus` so `PlayerTurnOrchestrator.TripleRoll` skips the default credit — else double-credit.
- Roll one die. Multiple your triple bonus by the number rolled
  - `DiceAction{Kind=ModifyTripleBonus, PayoutMultiplyByDie=true}` + card `SuppressDefault{SuppressTripleBonus}`. `ConditionType=None`.
  - ✅ Built — `DiceActionService.ModifyTripleBonus` rolls one die (`RollCardDice`) and applies `ApplyTripleBonus(factor: die)` to the holder (accumulator still +£500). **Requires** the card's `SuppressTripleBonus` (else the default credit + the card both fire).
- You do not receive your triple bonus
  - `DiceAction{Kind=ModifyTripleBonus, PayoutFactor=0}` + card `SuppressDefault{SuppressTripleBonus}`. `ConditionType=None`.
  - ✅ Built — `ApplyTripleBonus(factor:0)` pays nothing (`payout=0`) but still bumps the accumulator +£500 ("You do not receive a triple bonus this time"). The card's `SuppressTripleBonus` skips the default credit. Note: suppress *alone* would wrongly skip the accumulator — the `factor:0` action is what keeps it growing.
- Hand back half of your money or return a set to the bank
  - 2 groups (OR): [`MoneyAction{Pay, Bank, Basis=PercentOfOwnCash, Amount=50}`] OR [`PropertyAction{Kind=ReturnToBank, Set=true}`]. `CardOptionPrompt`. `ConditionType=None`.
  - ✅ Built — half-money via `MoneyAmountBasis.PercentOfOwnCash` (`RealiseAmount`: `player.Money × 50 / 100`); return-a-set via `PropertyAction{Set=true}`. The triple-bonus default still fires (not suppressed — this is a penalty, not a bonus modifier).
- Your cost to leave jail is reset to £50
  - `JailAction{Kind=ModifyLeaveFee, LeaveFeeSetTo=50, Target=Self}`. `ConditionType=None`.
  - ✅ Built — `JailActionService` `ModifyLeaveFee` sets `PlayerModel.JailCost` to the exact amount.
- Your triple bonus is doubled
  - `DiceAction{Kind=ModifyTripleBonus, PayoutFactor=2}` + card `SuppressDefault{SuppressTripleBonus}`. `ConditionType=None`.
  - ✅ Built — `ApplyTripleBonus(factor:2)` pays `base × 2` (accumulator still +£500). **Requires** the card's `SuppressTripleBonus` (else the default credit + the doubled card both fire).
- Return a set to the bank or pay £250 times the number of properties you own
  - 2 groups (OR): [**Property** return a whole set to the bank] OR [`MoneyAction{Pay, 250, FreeParking, PerUnit=PerProperty}`]. `CardOptionPrompt`.
  - ✅ Built — `PropertyAction{Kind=ReturnToBank, Set=true}` OR `MoneyAction{Pay, 250, FreeParking, PerUnit=PerProperty}`.
- Energy Crisis! Rent on utilities is multiplied by 10 until another double is rolled [GLOBAL EVENT]
  - Set `EventInfo.UtilityRentMultiplier = 10`.
  - ✅ Built — `GlobalEventActionService` (`Event=UtilityRent, Multiplier=10` → `StartUtilityRentEvent`). Clear-on-double already built.
- Rail Strike! There is no rent on any stations until another double is rolled [GLOBAL EVENT]
  - Set `EventInfo.StationRentMultiplier = 0`.
  - ✅ Built — `GlobalEventActionService` (maps the named event via `GlobalEventService`); clear-on-double built.


## Tax
You get a tax card from landing on a tax space.

**10 cards**: (all resolve-on-draw at the tax space, `ConditionType=None`, `SuppressDefault=true` unless they pay the normal tax. The "tax amount" is read via `AmountSource=TriggerAmount` — which must work in the **override-on-draw** path, §3.2)
- Pay triple tax or pay half and go to jail
  - 2 groups (OR): [`MoneyAction{Pay, FreeParking, AmountSource=TriggerAmount, Amount=3}`] OR [`MoneyAction{Pay, FreeParking, AmountSource=TriggerAmount, Amount=0.5}` + `MovementAction{AdvanceToIndex=100}`]. `CardOptionPrompt`. Card `SuppressDefault{SuppressTaxPayment}`.
  - ✅ Built — `TaxService.PayTax` assesses the tax up front and threads it as `CardActionContext.TriggerAmount` into `DrawCard` (override-on-draw context, §3.8); `MoneyAction.Amount` is now `decimal`, so the factor is `3` (triple) / `0.5` (half) directly (`RealiseAmount`: `TriggerAmount × Amount`). `SuppressTaxPayment` replaces the default tax.
- Tax payment is tripled
  - `MoneyAction{Pay, FreeParking, AmountSource=TriggerAmount, Amount=3}` + card `SuppressDefault{SuppressTaxPayment}`. `ConditionType=None`.
  - ✅ Built — Tax-amount threading (§3.8) + decimal factor; pays `tax × 3` to Free Parking, `FinancialReason.Tax` on the receipt/toast.
- Tax payment is doubled and every player pays
  - `MoneyAction{Pay, FreeParking, AmountSource=TriggerAmount, Amount=2, Target=Everyone}` + card `SuppressDefault{SuppressTaxPayment}`. `ConditionType=None`.
  - ✅ Built — the multi-target path realises `tax × 2` per player from the threaded trigger amount (holder included); `FinancialReason.Tax` on each receipt.
- Pay the tax due, but receive a free hotel (if there are hotels available)
  - Pay the normal tax (default runs) + **Building** grant a free hotel, no-op if none left.
  - ✅ Built — `BuildingAction{Kind=GrantHotel}` (bumps a chosen 4-house property → hotel; no-op if no hotel/eligible). Tax payment = the default.
- Tax payment is paid by the player rolling the lowest number
  - `MoneyAction{Pay, FreeParking, AmountSource=TriggerAmount, Amount=1, Target=DiceOffPlayer, DiceOff={Highest=false, IncludeHolder=false}}` + card `SuppressDefault{SuppressTaxPayment}`. `ConditionType=None`.
  - ✅ Built — `PlayerTarget.DiceOffPlayer`: `MoneyActionService.ApplyToDiceOffSubject` rolls the dice-off (lowest of the *other* players) and that player pays the threaded tax to Free Parking (`FinancialReason.Tax`). No opponent → no-op.
- Tax error. Receive what you have to pay
  - `MoneyAction{Receive, Bank, AmountSource=TriggerAmount, Amount=1}` + card `SuppressDefault{SuppressTaxPayment}`. `ConditionType=None`.
  - ✅ Built — receives `tax × 1` from the bank via the threaded trigger amount (`FinancialReason.Tax`).
- Tax payment is halved
  - `MoneyAction{Pay, FreeParking, AmountSource=TriggerAmount, Amount=0.5}` + card `SuppressDefault{SuppressTaxPayment}`. `ConditionType=None`.
  - ✅ Built — decimal factor `0.5` over the threaded tax amount (`FinancialReason.Tax`).
- Tax refund multiplied by one die.
  - `MoneyAction{Receive, Bank, AmountSource=TriggerAmount, Amount=1, DiceMultiplier=OneDie}` + card `SuppressDefault{SuppressTaxPayment}`. `ConditionType=None`.
  - ✅ Built — `RealiseAmount` applies the one-die multiplier after the trigger-amount base (`tax × die`); receive from bank (`FinancialReason.Tax`).
- The player rolling the lowest number pays the tax, and swaps places with you
  - Group of two (ordered): `MoneyAction{Pay, FreeParking, AmountSource=TriggerAmount, Amount=1, Target=DiceOffPlayer, DiceOff={Highest=false, IncludeHolder=false}}` then `MovementAction{Kind=Swap, Target=DiceOffPlayer}`. Card `SuppressDefault{SuppressTaxPayment}`. `ConditionType=None`.
  - ✅ Built — the money action resolves the dice-off (lowest *other* roller) and stashes it on `CardActionContext.DiceOffPlayerId`; the following Swap reads the same player back (so the money action must be authored first). Lowest pays the tax, then swaps places with the holder.
- Tax evasion! Pay a £500 fine and go to jail
  - `MoneyAction{Pay, 500, FreeParking}` + `MovementAction{AdvanceToIndex=100}`; suppress default tax.
  - ✅ Buildable now (fixed £500; `SuppressDefault` exists).


## GO Card
You get a GO card from landing on GO space.

**10 cards**: (drawn on landing GO; the "Default GO …" line is the `SuppressDefault` metadata, §3b)
- Advance to "GO". Keep until needed
  - `MovementAction{Kind=AdvanceToIndex, TargetIndex=0}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart | OnSpaceLand`. The card resolves GO itself (collect £200 — no `SuppressGoBonus`); card `SuppressDefault{SuppressBoardResolution}` stops the original landing re-resolving.
  - ✅ Built — written to `go.json`. (§12 advance-no-draw still deferred — advancing to GO also draws a GO card; the receipt-helper plan covers it later.)
- All players receive £200 times 2 dice. (Percentage applies; capped at cardholder player's %cap)
  - £200 × 2-dice granted from the bank to every player. `SuppressDefault{SuppressGoBonus}` (no £200 GO).
  - ✅ Built — written to `go.json` (`Money{Receive, 200, Bank, Target=Everyone, DiceMultiplier=TwoDice, PercentageApplies}` + `SuppressGoBonus`). **%cap is per *receiving* player** — the implemented multi-target behaviour, accepted over the per-cardholder cap.
- BAD NEWS. Purge 2 of your properties
  - **Building** purge 2 of the holder's own properties (chosen). Default GO runs.
  - ✅ Built — `BuildingActionService` (`Kind=Purge, Count=2`), written to `go.json`. Default GO runs.
- Immunity from swapping all money with another player. Keep until needed
  - **Immunity** card, keyed to the money-swap action. `ConditionType=ChoiceCardholderTurn`, no `CardTrigger` (so `CardTriggerService` never surfaces it — the immunity check is its only play path). Default GO runs.
  - ✅ Built — written to `go.json` (`Immunity{Immunity=SwappingMoney}`). Hooked in `MoneyActionService.SwapCash` via `CardImmunityService.CheckSwappingMoneyImmunity` (subject = the swap target), short-circuiting the cash swap when played.
- Receive £200 times one die
  - `MoneyAction{Receive, 200, Bank, DiceMultiplier=OneDie}` + `SuppressDefault{SuppressGoBonus}`.
  - ✅ Built — written to `go.json`.
- Receive £500 when passing GO anti-clockwise. Valid for 3 occasions
  - Held `MoneyAction{Receive, 500, Bank}`. `ConditionType=MetCardholderTurn`, `Trigger=OnPassGo` + `RequiredDirection=Backward` (anti-clockwise), `TurnsActive/Remaining=3`. Default GO runs.
  - ✅ Built — written to `go.json`. New `CardCondition.RequiredDirection` gates `OnPassGo` to anti-clockwise (`MatchingCardForTrigger` checks the subject's travel direction); duration via the multi-use lifecycle.
- Roll 2 dice. Multiple that value by the third die rolled this turn. Receive the final value multiplied by £200 (Percentage applies)
  - `MoneyAction{Receive, 200, Bank, DiceMultiplier=TwoDiceByThirdDie, PercentageApplies=true}` + `SuppressDefault{SuppressGoBonus}`.
  - ✅ Built — written to `go.json`. New `DiceMultiplier.TwoDiceByThirdDie` — `RollDiceMultiplier` rolls a fresh 2-dice total and multiplies by the turn's third die (`Cache.GetTurnDiceRoll().ThirdDie`).
- Swap spaces with any other player. Both players receive £200
  - `MovementAction{Kind=Swap, Target=ChosenPlayer}` (stashes the partner) + `Money{Receive, 200, Bank, Target=Self}` + `Money{Receive, 200, Bank, Target=ContextPlayer}`. `SuppressDefault{SuppressGoBonus}`.
  - ✅ Built — written to `go.json`. Generalised the shared-player slot (`context.DiceOffPlayerId` → `ContextPlayerId`); `ApplySwap` now stashes the chosen partner and new `PlayerTarget.ContextPlayer` (`MoneyActionService.ApplyToContextSubject`) grants them the £200.
- UNLUCKY! No money for landing on GO for the next 5 occasions.
  - Held `NoOpAction` + `SuppressDefault{SuppressGoBonus}`. `ConditionType=MetCardholderTurn`, `Trigger=OnLandGo`, `TurnsActive/Remaining=5`. The drawing turn keeps its £200; the next 5 GO landings are suppressed. ("Including this one" dropped — John's call.)
  - ✅ Built — written to `go.json`. Relies on the §11.1 fix (a kept card no longer suppresses at draw, `DrawCard` keep-branch returns `None`), so the drawing turn's £200 stands and the held card suppresses the next 5 via `OnLandGo` + the multi-use counter.
- When moving anti-clockwise, receive £200 for passing GO. Valid for 10 occasions
  - Held `MoneyAction{Receive, 200, Bank}`. `ConditionType=MetCardholderTurn`, `Trigger=OnPassGo` + `RequiredDirection=Backward` (anti-clockwise), `TurnsActive/Remaining=10`. Default GO runs.
  - ✅ Built — written to `go.json` (same `RequiredDirection` mechanism as the £500 anti-clockwise card).


## Just Visiting Card
You get a Just Visiting card from landing on Just Visiting space.

**10 cards**:
- Advance 2 spaces. Keep until needed
  - `MovementAction{Kind=MoveSpaces, Spaces=2}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart | OnSpaceLand`. Card `SuppressDefault{SuppressBoardResolution}`.
  - ✅ Built — written to `justVisiting.json` (the proven anytime-advance pattern).
- After your next move (roll or third die movement), move forward 23 spaces.
  - Held `MovementAction{Kind=MoveSpaces, Spaces=23}`. `ConditionType=MetCardholderTurn`, `Trigger=OnNextMove` (fires on roll *and* third-die movement), single-use.
  - ✅ Built — written to `justVisiting.json`. `OnNextMove` is wired into the orchestrator (fires after every move, per-player), so the held card fires on the holder's next move and is consumed.
- After your next move (roll or third die movement), go back 17 spaces.
  - Held `MovementAction{Kind=MoveSpaces, Spaces=-17}`. `ConditionType=MetCardholderTurn`, `Trigger=OnNextMove`, single-use.
  - ✅ Built — written to `justVisiting.json` (as the +23 card, `Spaces=-17`).
- Escaping prisoner drops £500. Finders keepers.
  - `MoneyAction{Receive, 500, Bank}`. `ConditionType=None`.
  - ✅ Built — written to `justVisiting.json`.
- Former prisoner agrees to steal the £200 bonus for passing GO clockwise from other players. Valid for 10 occasions
  - Held `MoneyAction{Receive, Bank, AmountSource=TriggerAmount, Amount=1}` + card `SuppressDefault{SuppressGoBonus}`. `ConditionType=MetAnyPlayerTurn`, `Trigger=OnOtherPassGo` + `RequiredDirection=Forward` (clockwise), `TurnsActive/Remaining=10`.
  - ✅ Built — written to `justVisiting.json`. `GoService` already consumes `OnOtherPassGo`'s `SuppressGoBonus` (skips the passer's bonus); the holder receives the threaded pass amount, `RequiredDirection` gates to clockwise. (£200 hardcoded in the text — a money tag would show the ×1 factor, not the trigger amount.)
- Immunity from taking a Go To Jail card when landing on Go To Jail. Play once, keep until needed
  - **Immunity** card, keyed to the Go-To-Jail draw. `ConditionType=ChoiceCardholderTurn`, no `CardTrigger`.
  - ✅ Built — written to `justVisiting.json` (`Immunity{Immunity=GoToJailCard}`). Hooked at the top of `JailService.GoToJail` via `CheckGoToJailCardImmunity` (subject = the lander): the Go-To-Jail card is **not** drawn, but the player **still goes to jail** (the default `SendPlayerToJail` runs) — matching the re-worded "immunity from *taking the card*", not from jail itself.
- Robbed by a prisoner who has escaped. £300 into free parking and return a property to the bank.
  - `MoneyAction{Pay, 300, FreeParking}` + **Property** return-to-bank (chosen). `ConditionType=None`.
  - ✅ Built — written to `justVisiting.json` (`Money{Pay, 300, FreeParking}` + `Property{ReturnToBank}`).
- Swap places with any other player in jail. Your jail fees to leave are also swapped.
  - `MovementAction{Kind=Swap, Target=ChosenPlayer, JailFilter=OnlyJailed}` (stashes the partner) + `JailAction{Kind=SwapLeaveFee, Target=ContextPlayer}`. `ConditionType=None`.
  - ✅ Built — written to `justVisiting.json`. `ApplySwap` now honours `action.JailFilter` (so `OnlyJailed` targets a jailed player; positions swap, and since `IsInJail` is derived from `BoardIndex` the holder lands in jail), and new `JailKind.SwapLeaveFee` exchanges the two players' `JailCost` via the stashed `ContextPlayer`.
- You befriend a prison guard. The next time you leave jail it will cost you nothing (no jail fee)
  - Held `JailAction{Kind=ModifyLeaveFee, FreeNextExit=true}`. `ConditionType=MetCardholderTurn`, `Trigger=OnInJail`, single-use.
  - ✅ Built — written to `justVisiting.json`. One-shot `PlayerModel.FreeNextJailExit` flag (set via `ModifyLeaveFee.FreeNextExit`, leaving `JailCost`/escalation intact); `PayJailFee` waives the next charge and clears it. `OnInJail` now also fires on `LeaveJailByPaying` (the command path) so it triggers on the voluntary exit. Cites new `RuleCode.Jail_FeeWaivedByCard` (rules.json §6.7c).
- You call a meeting. All other players not in jail advance to Just Visiting.
  - `MovementAction{Kind=GoToJustVisiting, Target=AllOthers, JailFilter=OnlyNotJailed}`. `ConditionType=None`.
  - ✅ Built — written to `justVisiting.json`. `MovementActionService` already filters `GoToJustVisiting` targets by `JailFilter`, so `OnlyNotJailed` is the not-in-jail exclusion — no new code.


## Free Parking Card
You get a Free Parking card from landing on Free Parking space.

**10 cards**: (drawn on landing FP; the "Default free parking …" line is the `SuppressDefault` metadata, §3b)
- Advance 6 spaces. Keep until needed
  - `MovementAction{Kind=MoveSpaces, Spaces=6}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnTurnStart | OnSpaceLand`. Card `SuppressDefault{SuppressBoardResolution}` (FP — the holder moves away, so the original FP landing must not resolve).
  - ✅ Built — written to `freeParking.json` (the proven anytime-advance pattern).
- All players receive from the bank 50% of the amount in free parking.
  - Each player receives 50% of `GameModel.FreeParkingAmount` from the bank. Default FP runs.
  - ✅ Built — written to `freeParking.json` (`Money{Receive, Bank, Target=Everyone, Basis=PercentOfFreeParkingPot, Amount=50}`; each player gets 50% of the pot from the bank, pot intact). Default FP runs.
- Each player, excluding you, must hand in a property into free parking (it does not get recorded and can be from a set you have handed in before). You will receive all properties in free parking.
  - `AllOthers` each hand a property in to FP (not recorded, no-op if none) + the holder receives all FP-held properties. Default FP runs.
  - ✅ Built — written to `freeParking.json` (`Property{HandInToFreeParking, Target=AllOthers}` + `Property{ReceiveAllFreeParking}`). Default FP runs.
- ID check fails. Swap places with the player in front of you travelling in the same direction. The player now on free parking proceeds as normal
  - `MovementAction{Kind=Swap, Target=NearestPlayerAhead, ResolveLandedSpaceForTarget=true}` + card `SuppressDefault{SuppressAllFreeParking}`. `ConditionType=None`.
  - ✅ Built — written to `freeParking.json`. New `PlayerTarget.NearestPlayerAhead` (board-relative §4 selector, same-direction preferred) + `MovementAction.ResolveLandedSpaceForTarget` (the swapped-in player resolves FP — "proceeds as normal"); the departed holder's FP is cancelled via `SuppressAllFreeParking`.
- Immunity from triple bonus being cancelled. Keep until needed
  - **Immunity** card, keyed to the triple-bonus-cancel action (the re-worded text dropped the triple-downgrade clause, so downgrade is **not** covered).
  - ✅ Built — written to `freeParking.json` (`Immunity{Immunity=CancelledTripleBonus}`). Hooked in `DiceActionService.ModifyTripleBonus` via `CheckCancelledTripleBonusImmunity` (subject = the bonus owner), **only when `factor == 0`** (the cancel); the bonus is left intact when played. `DowngradeTripleToDouble` is deliberately not hooked.
- Pass any retained card you have to the player rolling the lowest number on one die.
  - `CardTransferAction{Kind=Pass, DiceOff={Highest=true}}` — the holder picks **one** of their held cards to give to the **highest** one-die roller (John's steer; the card text reads "highest"). `ConditionType=None`.
  - ✅ Built — written to `freeParking.json`. New **Card** action category: `CardTransferAction` + `CardTransferActionService` (Pass/Steal), card chosen via a mandatory `CardOptionPrompt` over the hand; recipient via the dice-off picker.
- Steal any card from any player
  - `CardTransferAction{Kind=Steal}` — the holder picks a `ChosenPlayer`, then picks **which** of that player's cards to take (a `CardOptionPrompt` over the target's hand). `ConditionType=None`.
  - ✅ Built — written to `freeParking.json` (the same `CardTransferActionService` as the pass card).
- Your lucky day! First take £3000 (% applies) from free parking, the bank pays any shortfall. Then proceed as normal.
  - `MoneyAction{Receive, 3000, Counterparty=FreeParking, PercentageApplies=true}`; then default FP runs. `ConditionType=None`.
  - ✅ Built — written to `freeParking.json`. The bank-shortfall backstop is now intrinsic: `TransactionService.ApplyBalances` floors the FP pot at 0 (`Max(0, pot − amount)`), so a fixed take exceeding the pot credits the player in full with the bank covering the rest (and the old `uint` underflow is gone). Normal pot-capped takes are unaffected.
- Immunity from being the target of purging properties. Keep until needed
  - **Immunity** card, keyed to the (others-)purge action — immune the whole purge against you, not a single property.
  - ✅ Built — written to `freeParking.json` (`Immunity{Immunity=PurgedProperty}`). Hooked in `PurgingService.PurgeOthersProperty` via `CheckPurgingPropertyImmunity` (subject = the chosen owner), cancelling the purge when played. The self-purge path (`PurgeOwnProperty`) is not hooked.
- UNLUCKY! Someone else beat you to free parking while you were looking for a parking space. All money and properties in free parking are returned to the bank.
  - Clear the FP pot (money + properties) back to the bank. `SuppressDefault=true` (no fine/take/hand-in/purge).
  - ✅ Built — written to `freeParking.json` (`Property{ClearFreeParkingToBank}` — properties → bank + `FreeParkingAmount=0`); card `SuppressDefault{SuppressAllFreeParking}`.

## Go To Jail Card
You get a Go To Jail Card from landing on Go To Jail space.

**10 cards**: (drawn on landing Go-To-Jail; the "Default Go To Jail …" line is the `SuppressDefault` metadata, §3b)
- As a regular offender, your jail term is doubled (maximum of 6 turns) and must remain in jail for 3 turns. You still get to roll the dice on those 3 turns, but you cannot leave.
  - `JailAction{Kind=SendToJail, TurnsOverride=6, MinJailTurns=3}` + card `SuppressDefault{SuppressGoToJail}`. `ConditionType=None`.
  - ✅ Built — written to `goToJail.json`. The card states the concrete numbers (max 6, lock 3), so it's the existing `SendToJail`+override pattern: the card does the one jailing with `TurnsOverride=6`/`MinJailTurns=3`, and `SuppressGoToJail` stops the default Go-To-Jail double-send. The `MinJailTurns` lock gives "roll but can't leave" via `CanLeaveJail`.
- Corrupt judge steals one of your properties and puts it into free parking. It is not recorded.
  - `PropertyAction{Kind=HandInToFreeParking}` (the holder picks which property — "corrupt judge" is flavour; not the literal forced/involuntary read). Not recorded (doesn't touch `FPHandedInSets`). Default jail runs. `ConditionType=None`.
  - ✅ Built — written to `goToJail.json`. The existing holder-chooses hand-in; no involuntary mode needed.
- Dodgy judge facilitates a double in jail to become a triple (double upgraded to a triple only when in jail). Keep until needed
  - Held `DiceAction{Kind=ConvertDoubleToTriple}`. `ConditionType=ChoiceCardholderTurn`, `Trigger=OnRollDouble` + condition `JailFilter=OnlyJailed`.
  - ✅ Built — written to `goToJail.json`. The convert action + `OnRollDouble` already exist; new `CardCondition.JailFilter` (reusing the `JailFilter` enum) gates the condition to in-jail — `MatchingCardForTrigger` checks `subject.IsInJail` (same shape as the `RequiredDirection` gate).
- Fine of £1000 imposed by the judge. You do not go to jail, you stay where you are.
  - `MoneyAction{Pay, 1000, FreeParking}` + card `SuppressDefault{SuppressGoToJail}` (don't go to jail).
  - ✅ Built — written to `goToJail.json`.
- Friend in jail arranges for someone to purge 2 properties of your choice
  - **Building** purge 2 of the holder's own properties (chosen). Default jail runs.
  - ✅ Built — written to `goToJail.json` (`Building{Kind=Purge, Count=2}`). Default jail runs.
- Mishandled evidence. Do not go to jail, advance to just visiting.
  - `MovementAction{Kind=GoToJustVisiting, Target=Self}` + card `SuppressDefault{SuppressGoToJail}`.
  - ✅ Built — written to `goToJail.json`.
- Pay each player £1500 and go to jail (% applies)
  - `MoneyAction{Pay, 1500, Counterparty=EachPlayer, PercentageApplies=true}` + default jail runs.
  - ✅ Built — written to `goToJail.json` (no suppress — default jail runs).
- Wrongful arrest! Counter law suit wins you £2000 (% applies). Advance to just visiting.
  - `MoneyAction{Receive, 2000, Bank, PercentageApplies=true}` + `MovementAction{Kind=GoToJustVisiting}` + card `SuppressDefault{SuppressGoToJail}`.
  - ✅ Built — written to `goToJail.json`.
- You are caught by the police and bribe the arresting officer. Pay £100 times the number of properties you own. Do not go to jail, you stay where you are.
  - `MoneyAction{Pay, 100, FreeParking, PerUnit=PerProperty}` + card `SuppressDefault{SuppressGoToJail}`.
  - ✅ Built — written to `goToJail.json`.
- Immunity from returning a property to the bank. Keep until needed
  - **Immunity** card, keyed to the return-property-to-bank action.
  - ✅ Built — written to `goToJail.json` (`Immunity{Immunity=ReturningProperty}`). Hooked in `PropertyActionService.Relinquish` via `CheckReturningPropertyImmunity` (subject = the owner being made to relinquish), guarded to `ReturnToBank` only (hand-in-to-Free-Parking is not covered).