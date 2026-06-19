using System.Text.Json;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

/// <summary>
/// Card stats derived from the per-card receipts (cards-design.md §13.9). The only data the receipts
/// give us: counts per card type (taken vs played), totals, the never-played / instant-play split, the
/// immunity counts, and the most common played trigger / engagement. Everything is computed from
/// <see cref="CardTakenReceipt"/> (emitted only for keep-until-needed draws) and
/// <see cref="CardPlayedReceipt"/> (emitted for every resolved card — instant or held).
/// </summary>
public class CardStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        var events = snapshot.Turns.SelectMany(t => t.Events).ToList();

        var taken = events.OfType<CardTakenReceipt>()
            .Where(r => r.PlayerId == player.PlayerId).ToList();
        var played = events.OfType<CardPlayedReceipt>()
            .Where(r => r.PlayerId == player.PlayerId).ToList();

        var takenCardIds = taken.Select(r => r.CardId).ToHashSet();
        var playedCardIds = played.Select(r => r.CardId).ToHashSet();

        //Totals (occurrence counts). Never-played / instant-play test each occurrence's CardId against the
        //OTHER receipt set: a taken card whose CardId was never played, a played card never taken (resolve-on-draw).
        record.TotalCardsTaken = (uint)taken.Count;
        record.TotalCardsPlayed = (uint)played.Count;
        record.CardsNeverPlayed = (uint)taken.Count(r => !playedCardIds.Contains(r.CardId));
        record.InstantPlayCards = (uint)played.Count(r => !takenCardIds.Contains(r.CardId));
        record.ImmunityCardsTaken = (uint)taken.Count(r => r.IsImmunity);
        record.ImmunityCardsPlayed = (uint)played.Count(r => r.IsImmunity);

        //Per-type counts → JSON Dictionary<CardType,uint> (one map for taken, one for played).
        record.CardsTakenByTypeJson = JsonSerializer.Serialize(CountByType(taken.Select(r => r.CardType)));
        record.CardsPlayedByTypeJson = JsonSerializer.Serialize(CountByType(played.Select(r => r.CardType)));

        //Most common played trigger — split each played card's AllTriggers into individual flags and count.
        //Resolve-on-draw cards carry None (no bits) so they contribute nothing; null when none had a trigger.
        record.MostPlayedTrigger = played
            .SelectMany(r => SplitFlags(r.AllTriggers))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Select(g => (CardTrigger?)g.Key)
            .FirstOrDefault();

        //Most common engagement over EVERY played card (incl. resolve-on-draw → ResolveOnDraw); null when none.
        record.MostPlayedEngagement = played.Count == 0
            ? null
            : played.Select(r => MapEngagement(r.ConditionType))
                .GroupBy(e => e)
                .OrderByDescending(g => g.Count())
                .Select(g => (CardEngagement?)g.Key)
                .First();

        return record;
    }

    private static Dictionary<CardType, uint> CountByType(IEnumerable<CardType> types)
        => types.GroupBy(t => t).ToDictionary(g => g.Key, g => (uint)g.Count());

    /// <summary>The individual set flags of <paramref name="triggers"/> (excludes <see cref="CardTrigger.None"/>).</summary>
    private static IEnumerable<CardTrigger> SplitFlags(CardTrigger triggers)
        => Enum.GetValues<CardTrigger>()
            .Where(t => t != CardTrigger.None && triggers.HasFlag(t));

    /// <summary>Collapses a condition type to its engagement bucket: the two Met* → Forced, the two Choice* → Choice, None → ResolveOnDraw.</summary>
    private static CardEngagement MapEngagement(CardConditionType type)
        => type switch
        {
            CardConditionType.MetCardholderTurn or CardConditionType.MetAnyPlayerTurn => CardEngagement.Forced,
            CardConditionType.ChoiceCardholderTurn or CardConditionType.ChoiceAnyPlayerTurn => CardEngagement.Choice,
            _ => CardEngagement.ResolveOnDraw
        };
}