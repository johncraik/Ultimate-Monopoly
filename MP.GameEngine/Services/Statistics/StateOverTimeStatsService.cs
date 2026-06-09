using MP.GameEngine.Abstractions;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

/// <summary>
/// State-over-time scalars (<c>game-stats.md</c> §13.11): the player's peak net worth and
/// peak cash balance during the game, and the (earliest) turn each was reached. Walks every
/// turn's snapshot, valuing net worth via <see cref="StatisticsOrchestrator.CalculateNetWorth"/>.
/// </summary>
public class StateOverTimeStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        var playerId = player.PlayerId;

        long peakNetWorth = long.MinValue;
        var peakNetWorthTurn = 0;
        long peakBalance = -1;
        var peakBalanceTurn = 0;

        foreach (var turn in snapshot.Turns)
        {
            // Each turn's snapshot has its own PlayerModel instance — look it up by id.
            var snapshotPlayer = turn.Game.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (snapshotPlayer is null)
                continue;

            var turnNumber = (int)turn.Game.Metadata.TurnNumber;

            var netWorth = StatisticsOrchestrator.CalculateNetWorth(snapshotPlayer, turn.Game, snapshot.Board, snapshot.RoundingRule);
            if (netWorth > peakNetWorth)
            {
                peakNetWorth = netWorth;
                peakNetWorthTurn = turnNumber;
            }

            if (snapshotPlayer.Money > peakBalance)
            {
                peakBalance = snapshotPlayer.Money;
                peakBalanceTurn = turnNumber;
            }
        }

        record.PeakNetWorth = peakNetWorth == long.MinValue ? 0 : peakNetWorth;
        record.PeakNetWorthTurnNumber = peakNetWorthTurn;
        record.PeakBalance = peakBalance < 0 ? 0 : (uint)peakBalance;
        record.PeakBalanceTurnNumber = peakBalanceTurn;

        return record;
    }
}