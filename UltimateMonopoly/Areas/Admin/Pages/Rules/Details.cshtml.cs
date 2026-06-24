using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Helpers;
using UltimateMonopoly.Models.ViewModels;

namespace UltimateMonopoly.Areas.Admin.Pages.Rules;

[Authorize(Policy = "SystemAdminOnly")]
public class DetailsModel : PageModel
{
    private readonly RuleManagementService _rules;

    public DetailsModel(RuleManagementService rules) => _rules = rules;

    // The rule's structural identity (from the row's data-href). Point is bound as a string so an
    // empty value (a point-less rule) cleanly round-trips to null.
    [BindProperty(SupportsGet = true)] public int Section { get; set; }
    [BindProperty(SupportsGet = true)] public int Rule { get; set; }
    [BindProperty(SupportsGet = true)] public string? Point { get; set; }

    // The editable fields.
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string Description { get; set; } = "";
    [BindProperty] public bool IsHidden { get; set; }

    public GameRule RuleData { get; private set; } = default!;
    public string SectionName => PageRulesHelper.GetSection(Section)?.Section ?? $"Section {Section}";
    public string RuleId => RuleManagementService.RuleId(RuleData);

    private char? PointChar => string.IsNullOrEmpty(Point) ? null : Point[0];

    public async Task<IActionResult> OnGetAsync()
    {
        var rule = await _rules.GetRule(Section, Rule, PointChar);
        if (rule == null) return NotFound();

        RuleData = rule;
        Title = rule.Title;
        Description = rule.RuleDescription;
        IsHidden = rule.IsHidden;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var rule = await _rules.GetRule(Section, Rule, PointChar);
        if (rule == null) return NotFound();
        RuleData = rule;

        if (string.IsNullOrWhiteSpace(Title))
            ModelState.AddModelError(nameof(Title), "Title is required.");
        if (string.IsNullOrWhiteSpace(Description))
            ModelState.AddModelError(nameof(Description), "Description is required.");
        if (!ModelState.IsValid) return Page();

        await _rules.UpdateRule(Section, Rule, PointChar, Title, Description, IsHidden);
        TempData["Success"] = $"Rule {RuleId} updated.";
        return RedirectToPage("Index");
    }
}