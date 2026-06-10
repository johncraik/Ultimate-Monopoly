using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.Statistics;

namespace UltimateMonopoly.Models.Statistics;

/// <summary>How a stat value is formatted/rendered.</summary>
public enum StatKind { Money, Number, BoardIndex, PropertySet, FinancialReason, Bool, TriBool }

/// <summary>
/// Which direction is "good" — drives the single view's delta colour and the comparison
/// table's leader highlight. For <see cref="StatKind.Bool"/> it colours the Yes/No badge
/// (LowerBetter ⇒ true is bad). Categorical kinds use <see cref="Neutral"/> (no leader).
/// </summary>
public enum StatSentiment { HigherBetter, LowerBetter, Neutral }

/// <summary>
/// One value within a stat — the main value, a split half, or an inline detail badge.
/// <paramref name="Label"/> is the sub-label for split halves ("You rolled" / "Taken"); null otherwise.
/// </summary>
public sealed record StatPart(StatKind Kind, StatSentiment Sentiment, Func<PlayerStatRecord, object?> Value, string? Label = null);

/// <summary>
/// A single displayed stat. The single-player view renders it as a tile; the comparison table
/// renders it as a row (or, when <see cref="Secondary"/> is set, two rows).
/// </summary>
/// <param name="Sub">Free-text note shown in the same cell (e.g. "peaked on turn 23").</param>
/// <param name="Details">Categorical badges shown in the same cell (the largest-payment reason / property).</param>
/// <param name="Secondary">When set, the stat is a "split" of two values (dice number, loans).</param>
public sealed record StatDescriptor(
    string Label,
    string Icon,
    string Tone,
    StatPart Primary,
    Func<PlayerStatRecord, string?>? Sub = null,
    IReadOnlyList<StatPart>? Details = null,
    StatPart? Secondary = null);

/// <summary>A titled group of stats — a card in the single view, an accordion section in the table.</summary>
public sealed record StatSection(string Title, string Icon, string Tone, IReadOnlyList<StatDescriptor> Stats);

/// <summary>
/// The single source of truth for the player-stats display: every stat's label, icon, tone,
/// formatting kind, good-direction sentiment, value selector and any inline extras. Both the
/// single-player partial and the all-players comparison table render from this, so the two
/// can never drift. Pure presentation metadata over <see cref="PlayerStatRecord"/> — it lives
/// in the web layer (the engine record stays UI-agnostic).
/// </summary>
public static class StatCatalogue
{
    // Shorthands.
    private static StatPart Money(Func<PlayerStatRecord, object?> v, StatSentiment s) => new(StatKind.Money, s, v);
    private static StatPart Num(Func<PlayerStatRecord, object?> v, StatSentiment s) => new(StatKind.Number, s, v);

    public static readonly IReadOnlyList<StatSection> Sections =
    [
        new StatSection("Cash flow", "bi-cash-coin", "success",
        [
            new("Money earned", "bi-cash-stack", "success", Money(r => r.MoneyEarned, StatSentiment.HigherBetter)),
            new("Money spent", "bi-cart-dash", "danger", Money(r => r.MoneySpent, StatSentiment.LowerBetter)),
            new("Net cash flow", "bi-graph-up-arrow", "primary", Money(r => r.NetCashFlow, StatSentiment.HigherBetter)),
            new("Largest payment", "bi-arrow-down-circle", "warning", Money(r => r.LargestSinglePayment, StatSentiment.LowerBetter),
                Details:
                [
                    new StatPart(StatKind.FinancialReason, StatSentiment.Neutral, r => r.LargestSinglePaymentReason),
                    new StatPart(StatKind.BoardIndex, StatSentiment.Neutral, r => r.LargestSinglePaymentPropertyIndex)
                ]),
            new("Largest rent paid", "bi-house-down", "warning", Money(r => r.LargestRentPayment, StatSentiment.LowerBetter),
                Details: [new StatPart(StatKind.BoardIndex, StatSentiment.Neutral, r => r.LargestRentPaymentPropertyIndex)])
        ]),

        new StatSection("Spending", "bi-wallet2", "danger",
        [
            new("Acquiring property", "bi-bag-plus", "info", Money(r => r.SpentAcquiringProperty, StatSentiment.Neutral)),
            new("Building", "bi-houses", "info", Money(r => r.SpentBuilding, StatSentiment.Neutral)),
            new("Unmortgaging", "bi-unlock", "secondary", Money(r => r.SpentUnmortgaging, StatSentiment.Neutral)),
            new("Fines & penalties", "bi-exclamation-triangle", "danger", Money(r => r.SpentOnFines, StatSentiment.LowerBetter)),
            new("Leaving jail", "bi-lock", "danger", Money(r => r.SpentOnLeavingJail, StatSentiment.LowerBetter)),
            new("Repaying loans", "bi-arrow-return-left", "secondary", Money(r => r.SpentOnRepayingLoans, StatSentiment.Neutral)),
            new("Rent paid", "bi-house-dash", "danger", Money(r => r.RentPaid, StatSentiment.LowerBetter)),
            new("Given in deals", "bi-arrow-right", "secondary", Money(r => r.MoneyGivenInDeals, StatSentiment.Neutral))
        ]),

        new StatSection("Income", "bi-piggy-bank", "success",
        [
            new("Rent earned", "bi-house-heart", "success", Money(r => r.RentEarned, StatSentiment.HigherBetter)),
            new("Collected from GO", "bi-flag", "success", Money(r => r.MoneyCollectedFromGo, StatSentiment.HigherBetter),
                Sub: r => $"{r.TimesPassedGo:N0}× passed GO"),
            new("Building sell-backs", "bi-tag", "success", Money(r => r.MoneyFromSelling, StatSentiment.HigherBetter)),
            new("Mortgage payouts", "bi-cash-coin", "success", Money(r => r.MoneyFromMortgaging, StatSentiment.HigherBetter)),
            new("Free Parking", "bi-p-square", "success", Money(r => r.MoneyFromFreeParking, StatSentiment.HigherBetter)),
            new("Triple bonuses", "bi-dice-3", "success", Money(r => r.MoneyFromTriples, StatSentiment.HigherBetter)),
            new("Snake-eyes bonuses", "bi-dice-1", "success", Money(r => r.MoneyFromSnakeEyes, StatSentiment.HigherBetter)),
            new("Dice-number bonuses", "bi-123", "success", Money(r => r.MoneyFromDiceNumber, StatSentiment.HigherBetter)),
            new("From deals", "bi-arrow-left-right", "success", Money(r => r.MoneyFromDeals, StatSentiment.HigherBetter)),
            new("From bankruptcies", "bi-emoji-dizzy", "success", Money(r => r.MoneyFromBankruptPlayers, StatSentiment.HigherBetter)),
            new("From cards", "bi-card-heading", "success", Money(r => r.MoneyFromCards, StatSentiment.HigherBetter))
        ]),

        new StatSection("Property & sets", "bi-bank2", "warning",
        [
            new("Most profitable property", "bi-trophy", "success", new StatPart(StatKind.BoardIndex, StatSentiment.Neutral, r => r.MostProfitablePropertyIndex)),
            new("Least profitable property", "bi-emoji-frown", "danger", new StatPart(StatKind.BoardIndex, StatSentiment.Neutral, r => r.LeastProfitablePropertyIndex)),
            new("Most profitable set", "bi-collection", "success", new StatPart(StatKind.PropertySet, StatSentiment.Neutral, r => r.MostProfitablePropertySet)),
            new("Least profitable set", "bi-collection", "danger", new StatPart(StatKind.PropertySet, StatSentiment.Neutral, r => r.LeastProfitablePropertySet)),
            new("Max complete sets", "bi-stack", "primary", Num(r => r.MaxCompleteSets, StatSentiment.HigherBetter),
                Sub: r => $"peaked on turn {r.MaxCompleteSetsTurnNumber:N0}"),
            new("Properties acquired", "bi-plus-square", "success", Num(r => r.TotalPropertiesAcquired, StatSentiment.HigherBetter)),
            new("Properties lost", "bi-dash-square", "danger", Num(r => r.TotalPropertiesLost, StatSentiment.LowerBetter)),
            new("Properties purged", "bi-fire", "danger", Num(r => r.PropertiesPurged, StatSentiment.LowerBetter))
        ]),

        new StatSection("Dice & movement", "bi-dice-5", "primary",
        [
            new("Turn rolls", "bi-dice-5", "secondary", Num(r => r.TotalTurnRolls, StatSentiment.Neutral)),
            new("Doubles rolled", "bi-dice-2", "primary", Num(r => r.DoublesRolled, StatSentiment.HigherBetter)),
            new("Triples rolled", "bi-dice-3", "primary", Num(r => r.TriplesRolled, StatSentiment.HigherBetter)),
            new("Your dice number", "bi-123", "primary",
                Num(r => r.TimesYouRolledYourDiceNumber, StatSentiment.HigherBetter) with { Label = "You rolled" },
                Secondary: Num(r => r.TimesSomeoneRolledYourDiceNumber, StatSentiment.Neutral) with { Label = "Someone rolled" }),
            new("Direction changes", "bi-arrow-repeat", "secondary", Num(r => r.TimesChangedDirection, StatSentiment.Neutral)),
            new("Distance clockwise", "bi-arrow-clockwise", "secondary", Num(r => r.TotalDistanceTraveledClockwise, StatSentiment.Neutral)),
            new("Distance anti-clockwise", "bi-arrow-counterclockwise", "secondary", Num(r => r.TotalDistanceTraveledCounterClockwise, StatSentiment.Neutral)),
            new("Total distance", "bi-signpost-split", "secondary", Num(r => r.TotalDistanceTraveled, StatSentiment.Neutral)),
            new("Most landed-on space", "bi-geo-alt-fill", "info", new StatPart(StatKind.BoardIndex, StatSentiment.Neutral, r => r.MostLandedOnBoardIndex)),
            new("Landed on GO", "bi-flag-fill", "success", Num(r => r.TimesLandedOnGo, StatSentiment.HigherBetter)),
            new("Landed on Free Parking", "bi-p-circle-fill", "info", Num(r => r.TimesLandedOnFreeParking, StatSentiment.HigherBetter)),
            new("Landed on Tax", "bi-receipt", "danger", Num(r => r.TimesLandedOnTax, StatSentiment.LowerBetter))
        ]),

        new StatSection("Jail", "bi-lock-fill", "secondary",
        [
            new("Times sent to jail", "bi-box-arrow-in-down", "danger", Num(r => r.TimesSentToJail, StatSentiment.LowerBetter)),
            new("Total turns in jail", "bi-hourglass-split", "danger", Num(r => r.TotalJailTurns, StatSentiment.LowerBetter)),
            new("Left by paying", "bi-cash", "danger", Num(r => r.TimesLeftJailByPaying, StatSentiment.LowerBetter)),
            new("Left by dice", "bi-dice-6", "success", Num(r => r.TimesLeftJailByDice, StatSentiment.HigherBetter)),
            new("Left by card", "bi-card-text", "secondary", Num(r => r.TimesLeftJailByPlayingCard, StatSentiment.Neutral))
        ]),

        new StatSection("Free Parking", "bi-p-square-fill", "info",
        [
            new("Properties taken", "bi-box-arrow-down", "success", Num(r => r.TotalPropertiesTakenFromFP, StatSentiment.HigherBetter)),
            new("Properties handed in", "bi-box-arrow-up", "danger", Num(r => r.TotalPropertiesHandedInFP, StatSentiment.LowerBetter)),
            new("Money taken", "bi-cash-coin", "success", Money(r => r.MoneyFromFreeParking, StatSentiment.HigherBetter))
        ]),

        new StatSection("Loans & mortgages", "bi-bank", "warning",
        [
            new("Loans", "bi-bank", "warning",
                Num(r => r.TotalLoansTaken, StatSentiment.LowerBetter) with { Label = "Taken" },
                Secondary: Num(r => r.TotalLoansRepaid, StatSentiment.HigherBetter) with { Label = "Repaid" }),
            new("Total borrowed", "bi-cash-stack", "danger", Money(r => r.TotalLoanAmountTaken, StatSentiment.LowerBetter)),
            new("Loan repayments", "bi-arrow-repeat", "secondary", Num(r => r.TotalLoanRepayments, StatSentiment.Neutral)),
            new("Outstanding debt", "bi-exclamation-circle", "danger", Money(r => r.OutstandingLoanDebt, StatSentiment.LowerBetter)),
            new("Times mortgaged", "bi-house-lock", "danger", Num(r => r.TimesMortgaged, StatSentiment.LowerBetter)),
            new("Times unmortgaged", "bi-house-check", "success", Num(r => r.TimesUnmortgaged, StatSentiment.HigherBetter)),
            new("Mortgage fees (GO)", "bi-percent", "danger", Money(r => r.MortgageFeesPaid, StatSentiment.LowerBetter))
        ]),

        new StatSection("Endgame & peaks", "bi-flag-fill", "primary",
        [
            new("Bankrupted", "bi-x-octagon", "danger", new StatPart(StatKind.Bool, StatSentiment.LowerBetter, r => r.Bankrupted)),
            // Only meaningful if the player actually went bankrupt: Yes (chose to) = green,
            // No (forced) = red, N/A (never bankrupt) = grey.
            new("Voluntary bankruptcy", "bi-flag", "secondary", new StatPart(StatKind.TriBool, StatSentiment.HigherBetter, r => r.Bankrupted ? r.VoluntaryBankruptcy : (bool?)null)),
            new("Bankrupted by", "bi-cash-stack", "danger", Money(r => r.BankruptedByAmount, StatSentiment.LowerBetter)),
            new("Turns survived", "bi-clock-history", "primary", Num(r => r.TurnsSurvived, StatSentiment.HigherBetter)),
            new("Final balance", "bi-wallet2", "success", Money(r => r.FinalBalance, StatSentiment.HigherBetter)),
            new("Final net worth", "bi-piggy-bank-fill", "success", Money(r => r.FinalNetWorth, StatSentiment.HigherBetter)),
            new("Peak net worth", "bi-graph-up-arrow", "success", Money(r => r.PeakNetWorth, StatSentiment.HigherBetter),
                Sub: r => $"on turn {r.PeakNetWorthTurnNumber:N0}"),
            new("Peak balance", "bi-cash-coin", "success", Money(r => r.PeakBalance, StatSentiment.HigherBetter),
                Sub: r => $"on turn {r.PeakBalanceTurnNumber:N0}")
        ])
    ];
}