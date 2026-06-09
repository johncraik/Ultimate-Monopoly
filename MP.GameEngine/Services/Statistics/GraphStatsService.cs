using System.Text.Json;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

/// <summary>
/// Per-turn graph series (<c>game-stats.md</c> §13.13): balance, net worth, property count
/// and wealth rank — one value per snapshot, up to and including the turn the player went
/// bankrupt. Snapshots are start-of-turn, so the first snapshot that shows the player bankrupt
/// ends the series.
/// </summary>
public class GraphStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        var playerId = player.PlayerId;

        var balance = new List<uint>();
        var netWorth = new List<long>();
        var propertyCount = new List<int>();
        var wealthRank = new List<int>();

        foreach (var turn in snapshot.Turns)
        {
            var snapshotPlayer = turn.Game.Players.FirstOrDefault(p => p.PlayerId == playerId);

            // Start-of-turn snapshots: once the player shows bankrupt the series is complete.
            if (snapshotPlayer is null || snapshotPlayer.IsBankrupt)
                break;

            var worth = StatisticsOrchestrator.CalculateNetWorth(snapshotPlayer, turn.Game, snapshot.Board, snapshot.RoundingRule);

            balance.Add(snapshotPlayer.Money);
            netWorth.Add(worth);
            propertyCount.Add(turn.Game.GetOwnedProperties(playerId).Count);
            wealthRank.Add(WealthRank(worth, playerId, turn.Game, snapshot.Board, snapshot.RoundingRule));
        }

        record.BalanceOverTimeJson = JsonSerializer.Serialize(balance);
        record.NetWorthOverTimeJson = JsonSerializer.Serialize(netWorth);
        record.PropertyCountOverTimeJson = JsonSerializer.Serialize(propertyCount);
        record.WealthRankOverTimeJson = JsonSerializer.Serialize(wealthRank);

        return record;
    }

    /// <summary>
    /// The player's 1-based wealth rank among all players this turn by net worth (1 = richest).
    /// Players with equal net worth share the better rank.
    /// </summary>
    private static int WealthRank(long playerNetWorth, string playerId, GameModel game, Board board, GameRoundingRule roundingRule)
    {
        var richer = game.Players
            .Where(p => p.PlayerId != playerId)
            .Count(p => StatisticsOrchestrator.CalculateNetWorth(p, game, board, roundingRule) > playerNetWorth);

        return richer + 1;
    }
}