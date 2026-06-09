using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

/// <summary>
/// Property &amp; set economics (<c>game-stats.md</c> §13.4): per-property and per-set
/// net profit (the most/least profitable picks), the peak number of complete sets held,
/// and the count of properties acquired/lost over the game.
/// </summary>
public class PropertyStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        var playerId = player.PlayerId;

        var completedSets = ComputeCompleteSetStats(record, playerId, snapshot);
        ComputeProfitStats(record, playerId, snapshot, completedSets);
        ComputePropertyCounts(record, playerId, snapshot);

        // TODO: PropertiesPurged — awaits the purge service/receipt being wired (§13.4).
        record.PropertiesPurged = 0;

        return record;
    }

    /// <summary>
    /// Walks each turn's start-of-turn snapshot to capture both complete-set stats:
    /// the <b>peak</b> number of complete sets held at once (and the earliest turn it was
    /// reached), and the union of <b>every</b> set the player completed at any point — the
    /// latter is the eligibility list for the profitable-set picks (§13.4, "ever completed").
    /// <para>
    /// Completion follows the engine's own convention (<see cref="GameModel.CheckReservationRuleSetObtained"/>):
    /// own all of a set's properties, mortgaged counts, reserved doesn't. Stations and
    /// utilities are completable sets too (<c>onlyBuildable: false</c>).
    /// </para>
    /// </summary>
    private static HashSet<PropertySet> ComputeCompleteSetStats(PlayerStatRecord record, string playerId, CompleteGameSnapshot snapshot)
    {
        var everCompleted = new HashSet<PropertySet>();
        ushort maxCompleteSets = 0;
        uint maxCompleteSetsTurn = 1;   // turn 1 by default — the peak is 0 sets at the start

        foreach (var turn in snapshot.Turns)
        {
            var owned = turn.Game.GetOwnedProperties(playerId, includeReserved: false);
            var complete = PropertySetHelper.GetOwnedSets(playerId, owned, onlyBuildable: false);
            everCompleted.UnionWith(complete);

            if (complete.Count <= maxCompleteSets)
                continue;

            maxCompleteSets = (ushort)complete.Count;
            maxCompleteSetsTurn = turn.Game.Metadata.TurnNumber;
        }

        record.MaxCompleteSets = maxCompleteSets;
        record.MaxCompleteSetsTurnNumber = maxCompleteSetsTurn;
        return everCompleted;
    }

    /// <summary>
    /// Net profit per property and per set, accumulated across the whole game and attributed
    /// by board index.
    /// <para>
    /// Profit is a plain signed sum of the player's transaction amounts on that property:
    /// income (rent received, sell, mortgage) is positive and outlay (buy/auction, unreserve,
    /// build, unmortgage) is already negative, so the sum is exactly
    /// <c>(rent + sell + mortgage) − (buy + unreserve + build + unmortgage)</c>.
    /// </para>
    /// <para>
    /// The per-<b>property</b> pick spans every property the player owned (set completion not
    /// required); never-owned properties can't pollute it because the only property-indexed
    /// receipt for a property you don't own is rent you <i>paid</i>, which
    /// <see cref="CountsTowardProfit"/> excludes. The per-<b>set</b> pick is restricted to the
    /// sets the player actually completed (<paramref name="completedSets"/>), each scored by
    /// the cumulative profit across all of its properties.
    /// </para>
    /// </summary>
    private static void ComputeProfitStats(PlayerStatRecord record, string playerId,
        CompleteGameSnapshot snapshot, IReadOnlyCollection<PropertySet> completedSets)
    {
        var profitByIndex = snapshot.Turns
            .SelectMany(t => t.Events.OfType<FinancialTransactionReceipt>())
            .Where(fe => fe.PlayerId == playerId
                         && fe.CounterpartyPropertyIndex is not null
                         && CountsTowardProfit(fe))
            .GroupBy(fe => fe.CounterpartyPropertyIndex!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(fe => fe.Amount));

        if (profitByIndex.Count > 0)
        {
            record.MostProfitablePropertyIndex = profitByIndex.MaxBy(kv => kv.Value).Key;
            record.LeastProfitablePropertyIndex = profitByIndex.MinBy(kv => kv.Value).Key;
        }

        if (completedSets.Count == 0)
            return;

        var setProfits = completedSets
            .Select(set => (Set: set, Profit: PropertySetHelper.GetIndexes(set)
                .Sum(profitByIndex.GetValueOrDefault)))
            .ToList();

        record.MostProfitablePropertySet = setProfits.MaxBy(x => x.Profit).Set;
        record.LeastProfitablePropertySet = setProfits.MinBy(x => x.Profit).Set;
    }

    /// <summary>
    /// Whether a receipt contributes to a property's net profit. Rent only counts when
    /// <b>received</b> (the player is the owner); rent the player <i>paid</i> carries
    /// another player's property index and must not be attributed here. Every other listed
    /// reason only ever lands on the owner, so its signed amount is included as-is.
    /// </summary>
    private static bool CountsTowardProfit(FinancialTransactionReceipt fe) => fe.Reason switch
    {
        FinancialReason.Rent => fe.Amount > 0,
        FinancialReason.Sell
            or FinancialReason.Mortgage
            or FinancialReason.Purchase
            or FinancialReason.Auction
            or FinancialReason.UnReserve
            or FinancialReason.Build
            or FinancialReason.Unmortgage => true,
        _ => false
    };

    /// <summary>
    /// Counts the properties the player gained (+) and lost (−) over the game, from the
    /// <see cref="PropertyTransferReceipt.Value"/> on each ownership move.
    /// </summary>
    private static void ComputePropertyCounts(PlayerStatRecord record, string playerId, CompleteGameSnapshot snapshot)
    {
        var transfers = snapshot.Turns
            .SelectMany(t => t.Events.OfType<PropertyTransferReceipt>())
            .Where(pte => pte.PlayerId == playerId)
            .ToList();

        record.TotalPropertiesAcquired = (ushort)transfers.Where(t => t.Value > 0).Sum(t => t.Value);
        record.TotalPropertiesLost = (ushort)transfers.Where(t => t.Value < 0).Sum(t => -t.Value);
    }
}