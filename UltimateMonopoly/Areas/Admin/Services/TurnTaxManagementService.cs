using MP.GameEngine.Models;
using UltimateMonopoly.Services.GameEngine;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Admin-side wrapper over the singleton <see cref="TurnTaxService"/> for the SystemAdmin turn-tax editor
/// (C1 — Game Management). Reads the current brackets, persists new ones (or zeroes them all to disable),
/// and writes an <see cref="AdminLogService"/> entry for each change.
/// </summary>
public class TurnTaxManagementService
{
    private readonly TurnTaxService _turnTax;
    private readonly AdminLogService _adminLog;

    public TurnTaxManagementService(TurnTaxService turnTax, AdminLogService adminLog)
    {
        _turnTax = turnTax;
        _adminLog = adminLog;
    }

    public bool Enabled => _turnTax.Enabled;

    public TurnTax GetTurnTax() => _turnTax.GetTurnTax();

    public async Task Save(TurnTax tax)
    {
        await _turnTax.Save(tax);
        await _adminLog.LogTurnTaxUpdated(Describe(tax));
    }

    /// <summary>Zeroes every bracket — turn tax then no longer applies in any game.</summary>
    public async Task Disable()
    {
        await _turnTax.Save(new TurnTax());
        await _adminLog.LogTurnTaxUpdated("disabled the turn tax");
    }

    private static string Describe(TurnTax t)
    {
        if (!t.TurnTaxEnabled) return "disabled the turn tax";

        var brackets = new List<string>();
        if (t.LowerTaxBracket > 0 && t.LowerTaxRate > 0) brackets.Add($"{t.LowerTaxRate * 100:0.##}% over £{t.LowerTaxBracket:N0}");
        if (t.MiddleTaxBracket > 0 && t.MiddleTaxRate > 0) brackets.Add($"{t.MiddleTaxRate * 100:0.##}% over £{t.MiddleTaxBracket:N0}");
        if (t.UpperTaxBracket > 0 && t.UpperTaxRate > 0) brackets.Add($"{t.UpperTaxRate * 100:0.##}% over £{t.UpperTaxBracket:N0}");

        return $"updated the turn tax: {string.Join("; ", brackets)}";
    }
}