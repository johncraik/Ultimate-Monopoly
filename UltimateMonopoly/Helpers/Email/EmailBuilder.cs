using System.Net;
using System.Text;
using MP.GameEngine.Helpers.RuleSet;

namespace UltimateMonopoly.Helpers.Email;

/// <summary>
/// Builds matching plain-text and HTML bodies for outbound email from one set of section calls, so the two
/// never drift. Content is declared once (<see cref="Paragraph"/>, <see cref="Quote"/>, etc.) and
/// <see cref="Build"/> emits both — the HTML wrapped in a branded, email-client-safe shell (a gradient header
/// bar carrying the game name + a per-email caption, then the body).
/// <para>
/// This is transactional-email HTML, not app CSS: mail clients strip &lt;style&gt; blocks, classes, and CSS
/// variables, so everything is inline hex and table-based. The colour-variable convention that applies to the
/// site's own stylesheets deliberately does not apply here. All caller text is HTML-encoded inside the builder,
/// so call-sites pass raw strings and cannot leak unescaped user input.
/// </para>
/// </summary>
public sealed class EmailBuilder
{
    // Brand palette, mirrored from custom-bootstrap.scss ($primary / $info) as literal hex — see class remarks.
    private const string BrandStart = "#0b9fbd";   // $primary — teal
    private const string BrandEnd = "#8d94f0";     // $info    — periwinkle
    private const string HeaderCaption = "#eaf6f9"; // muted-white on the gradient bar
    private const string PageBg = "#f0f2f5";
    private const string CardBg = "#ffffff";
    private const string TextColour = "#1f2933";
    private const string SubtleColour = "#52606d";
    private const string MutedColour = "#7b8794";
    private const string RuleColour = "#d9dee3";
    private const string QuoteBg = "#f5f7fa";
    private const string QuoteBorder = "#c8d0d8";

    private const string PlainRule = "----------------------------------------";

    private readonly string _caption;
    private readonly List<(string Plain, string Html)> _sections = [];

    private EmailBuilder(string? caption) => _caption = caption?.Trim() ?? "";

    /// <summary>Starts a new email body. <paramref name="caption"/> is the per-email subtitle shown under the
    /// game name in the header bar (e.g. "Support", "New contact message"); pass null/empty for name only.</summary>
    public static EmailBuilder Create(string? caption = null) => new(caption);

    /// <summary>A body paragraph. Blank lines split into separate paragraphs; single newlines become line breaks.
    /// When <paramref name="emphasis"/> is true it renders as a bold, muted label (e.g. "For reference:").</summary>
    public EmailBuilder Paragraph(string text, bool emphasis = false)
    {
        var inner = ToHtmlInner(text);
        var html = emphasis
            ? $"<p style=\"margin: 0 0 6px; color: {SubtleColour};\"><strong>{inner}</strong></p>"
            : $"<p style=\"margin: 0 0 14px;\">{inner}</p>";
        _sections.Add((Normalise(text), html));
        return this;
    }

    /// <summary>A quoted / fenced block — used for a message or a reporter's original report. Rendered as a
    /// styled &lt;blockquote&gt; in HTML and a dash-fenced block in plain text.</summary>
    public EmailBuilder Quote(string text)
    {
        var html =
            $"<blockquote style=\"margin: 0 0 14px; padding: 10px 14px; border-left: 3px solid {QuoteBorder}; " +
            $"color: {SubtleColour}; background: {QuoteBg};\">{ToHtmlInner(text)}</blockquote>";
        var plain = $"{PlainRule}\n{Normalise(text)}\n{PlainRule}";
        _sections.Add((plain, html));
        return this;
    }

    /// <summary>A prominent call-to-action button linking to <paramref name="url"/> (e.g. a confirmation or
    /// reset link). Renders a gradient button in HTML — with a plain-text fallback link beneath, since some
    /// clients strip the button styling — and "<c>text: url</c>" in plain text.</summary>
    public EmailBuilder Button(string text, string url)
    {
        var urlEnc = WebUtility.HtmlEncode(url);
        var textEnc = WebUtility.HtmlEncode(Normalise(text));
        var html =
            "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin: 8px 0 14px;\"><tr>" +
            $"<td bgcolor=\"{BrandStart}\" style=\"background: {BrandStart}; background: linear-gradient(135deg, {BrandStart}, {BrandEnd}); border-radius: 6px;\">" +
            $"<a href=\"{urlEnc}\" style=\"display: inline-block; padding: 12px 24px; font-size: 15px; font-weight: bold; color: #ffffff; text-decoration: none;\">{textEnc}</a>" +
            "</td></tr></table>" +
            $"<p style=\"margin: 0 0 14px; color: {MutedColour}; font-size: 12px;\">Or paste this link into your browser:<br>" +
            $"<a href=\"{urlEnc}\" style=\"color: {BrandStart}; word-break: break-all;\">{urlEnc}</a></p>";
        _sections.Add(($"{Normalise(text)}:\n{url}", html));
        return this;
    }

    /// <summary>A horizontal rule between sections.</summary>
    public EmailBuilder Divider()
    {
        _sections.Add((PlainRule, $"<hr style=\"border: none; border-top: 1px solid {RuleColour}; margin: 20px 0;\">"));
        return this;
    }

    /// <summary>A closing line (e.g. "— The {game} Team"), spaced away from the body above it.</summary>
    public EmailBuilder SignOff(string text)
    {
        _sections.Add((Normalise(text), $"<p style=\"margin: 20px 0 0;\">{ToHtmlInner(text)}</p>"));
        return this;
    }

    /// <summary>A short, muted reference line.</summary>
    public EmailBuilder Reference(string code)
    {
        var encoded = WebUtility.HtmlEncode(code);
        _sections.Add(($"Reference: {code}",
            $"<p style=\"margin: 6px 0 0; color: {MutedColour}; font-size: 12px;\">Reference: {encoded}</p>"));
        return this;
    }

    /// <summary>A small, muted footer note (e.g. the "you're receiving this because…" disclaimer).</summary>
    public EmailBuilder Footer(string text)
    {
        _sections.Add((Normalise(text),
            $"<p style=\"margin: 14px 0 0; color: {MutedColour}; font-size: 12px;\">{ToHtmlInner(text)}</p>"));
        return this;
    }

    /// <summary>Renders the accumulated sections into matching plain-text and HTML bodies.</summary>
    public (string Plain, string Html) Build()
    {
        var plain = BuildPlain();
        var html = BuildHtml();
        return (plain, html);
    }

    private string BuildPlain()
    {
        var sb = new StringBuilder();
        sb.Append(RuleDictionary.GameName);
        if (_caption.Length > 0) sb.Append(" — ").Append(_caption);
        sb.Append("\n\n");
        sb.Append(string.Join("\n\n", _sections.Select(s => s.Plain)));
        return sb.ToString();
    }

    private string BuildHtml()
    {
        var caption = _caption.Length > 0
            ? $"<div style=\"font-size: 13px; color: {HeaderCaption}; margin-top: 2px;\">{WebUtility.HtmlEncode(_caption)}</div>"
            : "";
        var body = string.Join("", _sections.Select(s => s.Html));
        var gameName = WebUtility.HtmlEncode(RuleDictionary.GameName);

        // Table-based, inline-styled shell for maximum email-client compatibility. The gradient header carries a
        // solid bgcolor fallback for clients (e.g. Outlook) that ignore CSS gradients.
        return
            $"<div style=\"background: {PageBg}; padding: 24px 0; font-family: Arial, Helvetica, sans-serif;\">" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse: collapse;\">" +
            "<tr><td align=\"center\">" +
            "<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" " +
            $"style=\"width: 600px; max-width: 600px; border-collapse: collapse; background: {CardBg}; border-radius: 10px; overflow: hidden;\">" +
            "<tr>" +
            $"<td bgcolor=\"{BrandStart}\" style=\"background: {BrandStart}; background: linear-gradient(135deg, {BrandStart}, {BrandEnd}); padding: 24px 28px;\">" +
            $"<div style=\"font-size: 20px; font-weight: bold; color: #ffffff;\">{gameName}</div>" +
            caption +
            "</td>" +
            "</tr>" +
            "<tr>" +
            $"<td style=\"padding: 24px 28px; font-size: 15px; color: {TextColour}; line-height: 1.5;\">" +
            body +
            "</td>" +
            "</tr>" +
            "</table>" +
            "</td></tr>" +
            "</table>" +
            "</div>";
    }

    /// <summary>Normalises line endings for plain text (trims trailing whitespace, unifies newlines).</summary>
    private static string Normalise(string text)
        => (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();

    /// <summary>HTML-encodes free text, then maps blank lines to paragraph breaks and single newlines to
    /// &lt;br&gt; — the inner content for a &lt;p&gt; or &lt;blockquote&gt;.</summary>
    private static string ToHtmlInner(string text)
    {
        var encoded = WebUtility.HtmlEncode(Normalise(text));
        var paragraphs = encoded
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim('\n').Replace("\n", "<br>"));
        return string.Join("<br><br>", paragraphs);
    }
}