using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

/// <summary>
/// Jail stats (<c>game-stats.md</c> §13.6): times sent to jail, how the player left
/// (paying / card / dice), and the number of their own turns spent in jail.
/// </summary>
public class JailStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        var playerId = player.PlayerId;
        var events = snapshot.Turns.SelectMany(t => t.Events).ToList();

        // Sent to jail — the semantic PlayerEnteredJailReceipt, one per entry (JailService.SendPlayerToJail).
        record.TimesSentToJail = (uint)events
            .OfType<PlayerEnteredJailReceipt>()
            .Count(e => e.PlayerId == playerId);

        // Leaving jail. Every exit path advances the player off the jail space to Just Visiting,
        // so each leave is a PlayerMoved whose InitialBoardIndex is the jail space. The path is
        // told apart by what else the exit emitted: paying charges a JailFee, a release card emits a
        // CardPlayedReceipt flagged IsJailRelease — and whatever is left over was a doubles/triples escape.
        var leftByPaying = events
            .OfType<FinancialTransactionReceipt>()
            .Count(e => e.PlayerId == playerId && e.Reason == FinancialReason.JailFee && e.Amount < 0);

        // Card-driven exit: a Get-Out-of-Jail-Free (JailKind.Release) play, flagged on the receipt (M-06).
        // NOTE: IsJailRelease only populates for games whose receipts were serialised after the flag was
        // added — older games read 0 here and that exit falls into the dice bucket below; a recompute
        // can't backfill an unstored field (same caveat as the immunity stat).
        var leftByCard = events
            .OfType<CardPlayedReceipt>()
            .Count(e => e.PlayerId == playerId && e.IsJailRelease);

        var totalLeaves = events
            .OfType<PlayerMovedReceipt>()
            .Count(e => e.PlayerId == playerId && e.InitialBoardIndex == IndexHelper.JailSpace);

        record.TimesLeftJailByPaying = (uint)leftByPaying;
        record.TimesLeftJailByPlayingCard = (uint)leftByCard;
        record.TimesLeftJailByDice = (uint)Math.Max(0, totalLeaves - leftByPaying - leftByCard);

        // Jail turns — the player's own turn-starts spent in the jail space. The snapshot is
        // start-of-turn state, and CurrentPlayerId is whose turn it is, so a snapshot that is
        // this player's turn with them on the jail space is one turn they began in jail.
        record.TotalJailTurns = (uint)snapshot.Turns.Count(t =>
            t.Game.Metadata.CurrentPlayerId == playerId
            && t.Game.Players.FirstOrDefault(p => p.PlayerId == playerId)?.BoardIndex == IndexHelper.JailSpace);

        return record;
    }
}