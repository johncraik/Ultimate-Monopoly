using MP.GameEngine.Abstractions;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

/// <summary>
/// Endgame &amp; outcome stats (<c>game-stats.md</c> §13.11): bankruptcy (and how),
/// turns survived, and the player's final balance and net worth.
/// </summary>
public class EndgameStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        var playerId = player.PlayerId;

        // Bankruptcy — the PlayerBankruptedReceipt carries whether it was voluntary and the
        // shortfall amount that triggered it (null for a voluntary declaration).
        var bankruptcy = snapshot.Turns
            .SelectMany(t => t.Events.OfType<PlayerBankruptedReceipt>())
            .FirstOrDefault(e => e.PlayerId == playerId);

        record.Bankrupted = player.IsBankrupt || bankruptcy is not null;
        record.VoluntaryBankruptcy = bankruptcy?.VoluntaryBankruptcy ?? false;
        record.BankruptedByAmount = bankruptcy?.BankruptAmountBy;

        // Final standing. The supplied player is already the final-turn snapshot's player
        // (CompleteGameSnapshot.Players is the last turn), so its money is the final balance; the
        // last turn's game is what net worth values the owned properties against. A bankrupt
        // player's money is deliberately left intact (and they own nothing), so net worth is just cash.
        var finalGame = snapshot.Turns.LastOrDefault()?.Game;

        record.FinalBalance = player.Money;
        record.FinalNetWorth = finalGame is not null
            ? StatisticsOrchestrator.CalculateNetWorth(player, finalGame, snapshot.Board, snapshot.RoundingRule)
            : player.Money;

        // Turns survived — the turn they went bankrupt on, or the final game turn if they lasted.
        record.TurnsSurvived = (int)(bankruptcy?.TurnNumber ?? finalGame?.Metadata.TurnNumber ?? 0);

        return record;
    }
}