using System.Text.Json;

namespace MP.GameEngine.Helpers;

public static class MathHelper
{
    public static T? Median<T>(IEnumerable<T> values)
    {
        var list = values.OrderBy(x => x).ToList();
        return list.Count switch
        {
            0 => default,
            1 => list[0],
            _ => list.Count % 2 == 0 ? list[list.Count / 2] : list[(list.Count - 1) / 2]
        };
    }

    /// <summary>
    /// The most common value in <paramref name="values"/> (the mode). Ties resolve to the
    /// value encountered first. Returns <c>default</c> when the sequence is empty. Use for
    /// non-linear categoricals (enums, board indexes, bools) where a "typical" value is wanted.
    /// </summary>
    public static T? Mode<T>(IEnumerable<T> values)
        => values.GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    /// <summary>
    /// The least common value in <paramref name="values"/>. Ties resolve to the value
    /// encountered first. Returns <c>default</c> when the sequence is empty. The inverse of
    /// <see cref="Mode{T}"/> — the rarest categorical outcome.
    /// </summary>
    public static T? LeastCommon<T>(IEnumerable<T> values)
        => values.GroupBy(v => v)
            .OrderBy(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    /// <summary>
    /// Aggregates several per-turn numeric series (each a JSON array) element-wise, aligning by
    /// turn index. A series shorter than the longest contributes <c>0</c> for the turns it never
    /// reached (a game that ended early has no data point there). <paramref name="aggregator"/>
    /// reduces each turn's column (e.g. average / min / max) to a single value. Returns one value
    /// per turn up to the longest series; empty when there are no series.
    /// </summary>
    public static List<double> AggregateSeries(IEnumerable<string?> seriesJson, Func<IReadOnlyList<double>, double> aggregator)
    {
        var series = seriesJson
            .Select(j => string.IsNullOrWhiteSpace(j)
                ? []
                : JsonSerializer.Deserialize<List<double>>(j) ?? [])
            .ToList();

        if (series.Count == 0)
            return [];

        var length = series.Max(s => s.Count);
        var result = new List<double>(length);
        for (var i = 0; i < length; i++)
        {
            var index = i;
            //Missing data point (game ended before this turn) counts as 0.
            var column = series.Select(s => index < s.Count ? s[index] : 0d).ToList();
            result.Add(aggregator(column));
        }

        return result;
    }
}