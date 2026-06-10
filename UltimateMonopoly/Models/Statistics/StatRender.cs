using System.Net;
using JC.Core.Extensions;
using Microsoft.AspNetCore.Html;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Statistics;

namespace UltimateMonopoly.Models.Statistics;

/// <summary>
/// Shared rendering for a <see cref="StatPart"/> value — the bit most likely to drift between
/// the single-player tiles and the all-players comparison table, so both go through here.
/// Produces the formatted value fragment by <see cref="StatKind"/>, plus the numeric extraction
/// that drives leader-highlight (table) and delta (single view). Layout (tile vs cell) stays in
/// each Razor view; only the value rendering is shared.
/// </summary>
public static class StatRender
{
    private const string Dash = "<span class=\"text-body-secondary fs-5\">—</span>";

    public static string Money(long v) => v < 0 ? $"-£{Math.Abs(v):N0}" : $"£{v:N0}";
    public static string Num(long v) => v.ToString("N0");

    /// <summary>The part's value as a number for comparison — null for categorical kinds (no ordering).</summary>
    public static long? AsNumber(StatPart part, PlayerStatRecord r)
    {
        if (part.Kind is not (StatKind.Money or StatKind.Number))
            return null;
        var v = part.Value(r);
        return v is null ? null : Convert.ToInt64(v);
    }

    /// <summary>
    /// The standout value across players for a part: the lowest for LowerBetter, the highest
    /// otherwise (HigherBetter and Neutral both rank on "most"). Null when there's nothing to
    /// rank — categorical/no data, or everyone tied (no genuine standout, so all-equal rows
    /// don't light up).
    /// </summary>
    public static long? Leader(StatSentiment sentiment, IEnumerable<long?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (present.Count == 0) return null;

        var min = present.Min();
        var max = present.Max();
        if (min == max) return null;   // everyone equal — no standout

        return sentiment == StatSentiment.LowerBetter ? min : max;
    }

    /// <summary>The displayed value fragment for a part (money / number / space pill / set badge / reason badge / yes-no).</summary>
    public static IHtmlContent Value(StatPart part, PlayerStatRecord r, Board board) => part.Kind switch
    {
        StatKind.Money => new HtmlString(AsNumber(part, r) is { } m ? Money(m) : Dash),
        StatKind.Number => new HtmlString(AsNumber(part, r) is { } n ? Num(n) : Dash),
        StatKind.BoardIndex => new HtmlString(SpacePill((ushort?)part.Value(r), board)),
        StatKind.PropertySet => new HtmlString(SetBadge((PropertySet?)part.Value(r))),
        StatKind.FinancialReason => new HtmlString(ReasonBadge((FinancialReason?)part.Value(r))),
        StatKind.Bool => new HtmlString(YesNo((bool)part.Value(r)!, part.Sentiment)),
        StatKind.TriBool => new HtmlString(TriBool((bool?)part.Value(r), part.Sentiment)),
        _ => new HtmlString(Dash)
    };

    /// <summary>
    /// Single-view delta vs the comparison aggregate: coloured +/- by sentiment, grey "no change"
    /// on a tie, null when there's no comparison or the part isn't numeric.
    /// </summary>
    public static (string Cls, string Icon, string Text)? Delta(StatPart part, PlayerStatRecord stats, PlayerStatRecord? comparison)
    {
        if (comparison is null) return null;
        var cur = AsNumber(part, stats);
        var old = AsNumber(part, comparison);
        if (cur is null || old is null) return null;

        var diff = cur.Value - old.Value;
        if (diff == 0) return ("text-body-secondary", "bi-dash", "no change");

        var up = diff > 0;
        var cls = part.Sentiment == StatSentiment.Neutral ? "text-body-secondary"
            : (part.Sentiment == StatSentiment.HigherBetter) == up ? "text-success" : "text-danger";
        var mag = Math.Abs(diff);
        return (cls, up ? "bi-arrow-up-short" : "bi-arrow-down-short", part.Kind == StatKind.Money ? Money(mag) : Num(mag));
    }

    // ── value fragments ──

    private static string SpacePill(ushort? index, Board board)
    {
        if (index is null) return Dash;
        var set = PropertySetHelper.ResolveSet(index.Value);
        var banner = set is { } ps ? $"bg-prop-{ps.ToDisplayName().ToSlug()}" : "bg-secondary";
        string name;
        try { name = board.GetBoardSpace(index.Value).Name; }
        catch { name = $"Space {index}"; }
        return $"<span class=\"stat-space\"><span class=\"stat-space-banner {banner}\"></span>" +
               $"<span class=\"stat-space-name\">{WebUtility.HtmlEncode(name)}</span></span>";
    }

    private static string SetBadge(PropertySet? set)
        => set is { } ps
            ? $"<span class=\"badge text-bg-prop-{ps.ToDisplayName().ToSlug()} fs-6\">{WebUtility.HtmlEncode(ps.ToDisplayName())}</span>"
            : Dash;

    private static string ReasonBadge(FinancialReason? reason)
        => reason is { } fr
            ? $"<span class=\"badge text-bg-secondary fs-6\">{WebUtility.HtmlEncode(fr.ToDisplayName())}</span>"
            : Dash;

    private static string YesNo(bool value, StatSentiment sentiment)
    {
        var good = sentiment switch
        {
            StatSentiment.HigherBetter => (bool?)value,
            StatSentiment.LowerBetter => !value,
            _ => null
        };
        var cls = good is null ? "secondary" : good.Value ? "success" : "danger";
        return $"<span class=\"badge text-bg-{cls} fs-6\">{(value ? "Yes" : "No")}</span>";
    }

    /// <summary>Tri-state bool — null renders a grey "N/A" (e.g. voluntary bankruptcy when the player never went bankrupt).</summary>
    private static string TriBool(bool? value, StatSentiment sentiment)
    {
        if (value is null) return "<span class=\"badge text-bg-secondary fs-6\">N/A</span>";

        var good = sentiment switch
        {
            StatSentiment.HigherBetter => (bool?)value.Value,
            StatSentiment.LowerBetter => !value.Value,
            _ => null
        };
        var cls = good is null ? "secondary" : good.Value ? "success" : "danger";
        return $"<span class=\"badge text-bg-{cls} fs-6\">{(value.Value ? "Yes" : "No")}</span>";
    }
}