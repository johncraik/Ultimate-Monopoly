namespace MP.GameEngine.Enums;

/// <summary>
/// The citable subset of the rulebook — the rules the engine applies as a
/// distinct turn-resolution branch and surfaces on the host "Rules Occurred this
/// Turn" panel via <c>IRuleEmitter.Cite</c>. See <c>design-docs/rule-citation.md</c>.
///
/// This is NOT the whole rulebook. <c>rules.json</c> (the public-facing catalogue,
/// written from <c>game-rules.md</c>) holds every rule, including non-citable ones
/// (marked <c>"&lt;NONE&gt;"</c>). A rule earns a code here only when the engine has a
/// distinct branch that statement governs (rule-citation.md §6) — player-initiated
/// portfolio commands, their blocking gates, pure setup, and exposition do not.
///
/// Ordered by game-rules.md section. The display number (3.2.a) lives in the
/// catalogue, never here — the enum name is the stable identity (§5). Every value
/// must have a rules.json entry (lockstep test, §10). Cards/Deals add their own
/// codes with those subsystems.
/// </summary>
public enum RuleCode
{
    // ── Default Monopoly Rules ──────────────────────────────────────────────
    Default_BuyRequiresPassingGo,        // Lands on unowned before passing GO → no buy/auction. BoardService gate.
    Default_NoRentWhileOwnerJailed,      // Owner in jail collects no rent. TransactionService.PayRent.
    Default_FinesToFreeParking,          // Tax / jail fee / fines go to the FP pot, not the bank. TransactionService.

    // ── Dice Rolls ──────────────────────────────────────────────────────────
    Roll_ThirdDieMovesOthers,            // After the roller moves, the third die moves every other player. ResolveThirdDieMovement.
    Roll_DiceNumberByOther,              // Another player rolled your number → £100 + a third card. PlayerService.ResolveDiceNumber.
    Roll_DiceNumberBySelf,               // You rolled your own number → £100 + £100 from each + a third card. PlayerService.ResolveDiceNumber.

    // ── Movement ────────────────────────────────────────────────────────────
    Move_DirectionLockedUntilGo,         // Direction can't change until you've passed GO once. PlayerModel.FlipDirection.

    // ── Double Dice Rolls ───────────────────────────────────────────────────
    Double_OneSnakeEyes,                 // Double 1 → collect £500. DoubleEffects + ReceiveSnakeEyes.
    Double_TwoOthersMissTurn,            // Double 2 → every other player misses their next turn. DoubleEffects.
    Double_ThreeForwardBack,             // Double 3 → roller forward 3 (act), then back 3 (act). DoubleEffects.
    Double_FourOthersForwardBack,        // Double 4 → every other player forward 4 (act), then back 4 (act). DoubleEffects.
    Double_FiveForwardOthersBack,        // Double 5 → roller forward 10; others back 10; roller gets no extra roll + misses a turn. DoubleEffects.
    Double_SixBackTwelve,                // Double 6 → roller back 12 (act). DoubleEffects.
    Double_DirectionChange,              // After a double the roller changes direction (if past GO). PlayerTurnOrchestrator → FlipDirection.
    Double_ThreeInRowToJail,             // Three doubles in a row → straight to jail, turn ends. PlayerTurnOrchestrator.

    // ── Triple Dice Rolls ───────────────────────────────────────────────────
    Triple_Bonus,                        // Triple → the triple bonus (+£500 each time). PlayerTurnOrchestrator (pending payout wiring).
    Triple_MovesCombinedNoOthers,        // Triple → roller moves the combined total; no third-die movement. PlayerTurnOrchestrator.
    Triple_NoDirectionChange,            // Unlike a double, a triple does NOT change direction. PlayerTurnOrchestrator.
    Triple_ThreeInRowToJail,             // Three triples in a row → straight to jail. PlayerTurnOrchestrator.

    // ── GO Space ────────────────────────────────────────────────────────────
    Go_PassClockwise,                    // Passing GO clockwise → £200. GoService.CollectGoMoney.
    Go_PassAntiClockwise,                // Passing GO anti-clockwise → £100. GoService.CollectGoMoney.
    Go_LandOn,                           // Landing on GO → £200 either direction. GoService.LandOnGo.

    // ── Go To Jail Space ────────────────────────────────────────────────────
    GoToJail_SendToJail,                 // Landing on Go To Jail sends you to jail. JailService.GoToJail.

    // ── Jail ────────────────────────────────────────────────────────────────
    Jail_LeaveByDouble,                  // Rolling a double (or triple) releases you from jail. JailService.CheckAndLeaveJail.
    Jail_ThreeTurnLimit,                 // Three turns max — on the third you must pay to leave. JailService.ForcePlayerToLeaveJail.
    Jail_FeeEscalates,                   // The jail fee grows 50% each turn served. JailService.LeaveJailByPaying.

    // ── Free Parking ────────────────────────────────────────────────────────
    FreeParking_PayDiceDifference,       // Empty pot or no properties → pay (dice difference × 100) in. FreeParkingService case A.
    FreeParking_NoPayOnDouble,           // A double/triple makes the difference 0 → pay nothing. FreeParkingService (diff == 0).
    FreeParking_TakeCap,                 // Take up to £1000 from the pot — £2000 with a double hotel. FreeParkingService.TakeFromFreeParking.
    FreeParking_HandInEligibility,       // Hand in one property not from a built-on set / not already handed in. FreeParkingService.
    FreeParking_HandInTrackedPerSet,     // A hand-in marks that whole set used for you for the game. FreeParkingService + FPHandedInSets.
    FreeParking_TakeProperties,          // You also sweep any properties sitting in Free Parking. FreeParkingService.
    FreeParking_PurgeWhenNoneEligible,   // No eligible property → purge one, still take money + FP properties. FreeParkingService case C (pending PurgeService).

    // ── Loans ───────────────────────────────────────────────────────────────
    Loan_CoversShortfall,                // A loan covers exactly the shortfall, rounded up to £50. LoanService.TakeLoanForShortfall.
    Loan_KeepUpTo200,                    // You keep up to £200; only cash above it goes toward the debt. LoanService.
    Loan_MaxThree,                       // At most three outstanding loans. LoanService / PlayerModel.CanTakeLoan.
    Loan_RepayInstalmentOnGo,            // Passing GO repays 10% of the total originally borrowed. GoService → LoanService.ForcedRepayLoans.
    Loan_RepaidOldestOverpaymentLost,    // Instalment clears the oldest loan first; overpayment is lost, not carried. LoanService.

    // ── Mortgaging ──────────────────────────────────────────────────────────
    Mortgage_FeeOnGo,                    // Passing GO charges 20% of purchase cost on each mortgaged property. PropertyService.PayMortgageFee.
    Mortgage_NoSetRentWhileMortgaged,    // A mortgaged property drops the whole set's rent until unmortgaged. PropertyService.NormaliseRentLevels.

    // ── Reserved Properties ─────────────────────────────────────────────────
    Reserved_NoSetUntilAllCan,           // Nobody holds a complete set until everyone can — you reserve instead. The reserve mechanic.
    Reserved_ReserveFinalProperty,       // Landing on your set-completer (rule active) → reserve at 50%. PropertyService.ReserveProperty.
    Reserved_PropertyInert,              // A reserved property earns no rent and can't be bought by others. NormaliseRentLevels / PayRent.
    Reserved_MechanicEnds,               // Once a player breaks through to a full set, the reserve rule ends for all. CheckReservationRule[SetObtained].

    // ── Auctions ────────────────────────────────────────────────────────────
    Auction_Trigger,                     // Declining or not affording a property you landed on sends it to auction. PropertyService.UnownedProperty.
    Auction_MinimumBidHalfPrice,         // The auction opens at half the purchase price (the reserve floor). AuctionService.
    Auction_ForcedLastSurvivor,          // Last bidder standing wins at the current bid — even the decliner, forced at the floor. AuctionService.
    Auction_NobodyCanAfford,             // Nobody affords the floor → auction cancelled, property stays with the bank. AuctionService.

    // ── Stations ────────────────────────────────────────────────────────────
    Station_PriceScales,                 // A station's price scales £200/250/300/400 with stations owned. PropertyService.GetPropertyCost.
    Station_MortgagedCountsForPrice,     // Mortgaged stations still push up the next station's price (opposite of rent). PropertyService.GetPropertyCost.

    // ── Utilities ───────────────────────────────────────────────────────────
    Utility_RentIsDiceTimesMultiplier,   // Utility rent is the multiplier × the dice that moved you there. PropertyService.PropertyRent.
    Utility_DiceDependsOnArrival,        // Own roll → both main dice; third-die move → the third die alone. PropertyService.PropertyRent.
    Utility_PairMultiplier,              // Owning one utility vs both changes the multiplier. NormaliseRentLevels.

    // ── Bankruptcy ── (pending BankruptcyService + the game-conclusion seam) ──
    Bankruptcy_Declared,                 // Can't pay with nothing left to raise — or voluntary "I quit". BankruptcyService (unbuilt).
    Bankruptcy_AssetsToBank,             // All money and properties return to the bank, never to a player. PropertyTransferService.Bankrupt.
    Bankruptcy_CreditorPaidByBank,       // A creditor of a rent/fine debt is paid in full by the bank. BankruptcyService (unbuilt).
    Bankruptcy_LastPlayerWins,           // The last player not bankrupt wins the game. BankruptcyService (unbuilt).

    // ── Cards & Deals ───────────────────────────────────────────────────────
    // Deferred — these subsystems add their own codes when built (the "collect a
    // <type> card" lines, percentage tiers, NOPE, deal offer/settle, card-driven
    // purges/swaps). rules.json carries their text now, coded "<NONE>" until wired.
}