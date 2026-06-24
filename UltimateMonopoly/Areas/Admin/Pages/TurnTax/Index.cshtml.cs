using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Models;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.TurnTax;

[Authorize(Policy = "SystemAdminOnly")]
public class IndexModel : PageModel
{
    private readonly TurnTaxManagementService _turnTax;

    public IndexModel(TurnTaxManagementService turnTax) => _turnTax = turnTax;

    public bool Enabled => _turnTax.Enabled;

    // Thresholds in £; rates as percentages (converted to/from the stored float 0–1).
    [BindProperty] public uint? LowerThreshold { get; set; }
    [BindProperty] public double? LowerRate { get; set; }
    [BindProperty] public uint? MiddleThreshold { get; set; }
    [BindProperty] public double? MiddleRate { get; set; }
    [BindProperty] public uint? UpperThreshold { get; set; }
    [BindProperty] public double? UpperRate { get; set; }

    public void OnGet() => Load();

    public async Task<IActionResult> OnPostAsync()
    {
        Validate();
        if (!ModelState.IsValid) return Page();

        await _turnTax.Save(BuildTax());
        TempData["Success"] = "Turn tax updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        await _turnTax.Disable();
        TempData["Success"] = "Turn tax disabled.";
        return RedirectToPage();
    }

    private void Load()
    {
        var t = _turnTax.GetTurnTax();
        LowerThreshold = t.LowerTaxBracket;
        LowerRate = Math.Round(t.LowerTaxRate * 100, 2);
        MiddleThreshold = t.MiddleTaxBracket;
        MiddleRate = Math.Round(t.MiddleTaxRate * 100, 2);
        UpperThreshold = t.UpperTaxBracket;
        UpperRate = Math.Round(t.UpperTaxRate * 100, 2);
    }

    private MP.GameEngine.Models.TurnTax BuildTax() => new()
    {
        LowerTaxBracket = LowerThreshold ?? 0,
        LowerTaxRate = (float)((LowerRate ?? 0) / 100.0),
        MiddleTaxBracket = MiddleThreshold ?? 0,
        MiddleTaxRate = (float)((MiddleRate ?? 0) / 100.0),
        UpperTaxBracket = UpperThreshold ?? 0,
        UpperTaxRate = (float)((UpperRate ?? 0) / 100.0)
    };

    private void Validate()
    {
        ValidateBracket(nameof(LowerRate), LowerThreshold ?? 0, LowerRate ?? 0);
        ValidateBracket(nameof(MiddleRate), MiddleThreshold ?? 0, MiddleRate ?? 0);
        ValidateBracket(nameof(UpperRate), UpperThreshold ?? 0, UpperRate ?? 0);

        // Thresholds must increase across the active (fully-set) brackets: lower < middle < upper.
        var active = new List<(string Name, uint Threshold)>();
        if (IsActive(LowerThreshold, LowerRate)) active.Add(("Lower", LowerThreshold!.Value));
        if (IsActive(MiddleThreshold, MiddleRate)) active.Add(("Middle", MiddleThreshold!.Value));
        if (IsActive(UpperThreshold, UpperRate)) active.Add(("Upper", UpperThreshold!.Value));

        for (var i = 1; i < active.Count; i++)
            if (active[i].Threshold <= active[i - 1].Threshold)
                ModelState.AddModelError(string.Empty,
                    $"Bracket thresholds must increase: {active[i - 1].Name} (£{active[i - 1].Threshold:N0}) must be below {active[i].Name} (£{active[i].Threshold:N0}).");
    }

    private static bool IsActive(uint? threshold, double? rate) => (threshold ?? 0) > 0 && (rate ?? 0) > 0;

    private void ValidateBracket(string rateKey, uint threshold, double rate)
    {
        // A bracket must be fully set or fully empty — never half (a threshold of 0 with a rate would
        // otherwise tax the whole balance).
        if ((threshold > 0) != (rate > 0))
            ModelState.AddModelError(rateKey, "Set both the threshold and the rate, or leave both at 0.");

        if (rate is < 0 or > 100)
            ModelState.AddModelError(rateKey, "Rate must be between 0 and 100%.");
    }
}