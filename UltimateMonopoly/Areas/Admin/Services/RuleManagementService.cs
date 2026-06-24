using JC.Core.Extensions;
using UltimateMonopoly.Helpers;
using UltimateMonopoly.Models.ViewModels;
using UltimateMonopoly.Services;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Admin-side wrapper over <see cref="RuleCatalog"/> for the SystemAdmin rules editor (C1 — Game
/// Management). Edits are limited to what <see cref="RuleCatalog.TryUpdateRules"/> allows — title,
/// description, and hidden state — never a rule's structural identity (section / rule / point / code),
/// which the engine cites against. Every save writes an <see cref="AdminLogService"/> entry.
/// </summary>
public class RuleManagementService
{
    private readonly RuleCatalog _ruleCatalog;
    private readonly AdminLogService _adminLog;

    public RuleManagementService(RuleCatalog ruleCatalog, AdminLogService adminLog)
    {
        _ruleCatalog = ruleCatalog;
        _adminLog = adminLog;
    }

    /// <summary>A rule's display id, e.g. "0.1" or "4.1.a" — used as the row key and the admin-log target.</summary>
    public static string RuleId(GameRule rule)
        => rule.Point.HasValue ? $"{rule.Section}.{rule.Rule}.{rule.Point}" : $"{rule.Section}.{rule.Rule}";

    /// <summary>
    /// Every rule, optionally filtered by an in-memory search (title / description / rule-code / section
    /// name). Ordered by section, then by hidden (hidden last), then rule → point — so the page can group
    /// straight into a table per section. Shared by the page GET and the table-partial GET.
    /// </summary>
    public async Task<List<GameRule>> GetRules(string? search)
    {
        var rules = await _ruleCatalog.GetRules();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            rules = rules.Where(r =>
                r.Title.ToLowerInvariant().Contains(s)
                || r.RuleDescription.ToLowerInvariant().Contains(s)
                || (r.RuleCode?.ToDisplayName().ToLowerInvariant().Contains(s) ?? false)
                || (PageRulesHelper.GetSection(r.Section)?.Section.ToLowerInvariant().Contains(s) ?? false))
                .ToList();
        }

        return rules
            .OrderBy(r => r.Section)
            .ThenBy(r => r.IsHidden)
            .ThenBy(r => r.Rule)
            .ThenBy(r => r.Point)
            .ToList();
    }

    public async Task<GameRule?> GetRule(int section, int rule, char? point)
    {
        var rules = await _ruleCatalog.GetRules();
        return rules.FirstOrDefault(r => r.Section == section && r.Rule == rule && r.Point == point);
    }

    /// <summary>Updates the title / description / hidden state of a single rule and logs it.</summary>
    public async Task<bool> UpdateRule(int section, int rule, char? point, string title, string description, bool isHidden)
    {
        var existing = await GetRule(section, rule, point);
        if (existing == null) return false;

        // A fresh copy carrying the same identity (incl. RawRuleCode, so TryUpdateRules matches it) —
        // avoids mutating the cached instance outside the catalogue's own update path.
        var updated = new GameRule
        {
            Section = existing.Section,
            Rule = existing.Rule,
            Point = existing.Point,
            RawRuleCode = existing.RawRuleCode,
            Title = title.Trim(),
            RuleDescription = description.Trim(),
            IsHidden = isHidden
        };

        if (!await _ruleCatalog.TryUpdateRules([updated])) return false;

        await _adminLog.LogRulesUpdated(RuleId(updated), updated.Title);
        return true;
    }
}