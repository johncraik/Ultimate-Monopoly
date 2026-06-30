using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Settings;

[Authorize(Policy = "SystemAdminOnly")]
public class IndexModel : PageModel
{
    private readonly SettingsManagementService _settings;

    public IndexModel(SettingsManagementService settings) => _settings = settings;

    [BindProperty]
    public GameSettings Settings { get; set; } = new();

    public void OnGet() => Settings = _settings.Get();

    public async Task<IActionResult> OnPostSaveAsync()
    {
        Normalise();
        Validate();
        if (!ModelState.IsValid) return Page();

        await _settings.Save(Settings);
        TempData["Success"] = "Game settings saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevertAsync()
    {
        await _settings.RevertToDefaults();
        TempData["Success"] = "Game settings reverted to defaults.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRecomputeAsync()
    {
        var count = await _settings.RecomputeStatistics();
        TempData["Success"] = count == 0
            ? "No statistics needed recomputing — nothing with a fully intact history to rebuild."
            : $"Statistics recompute queued — {count} player stat record(s) cleared and re-queued.";
        return RedirectToPage();
    }

    // A disabled toggle's retention field is irrelevant — null it server-side so a stale posted value
    // (or a disabled input that didn't post) can't persist, and the validation below is skipped for it.
    private void Normalise()
    {
        if (!Settings.EnableCleanup) Settings.CleanupRetentionMonths = null;
        if (!Settings.EnableAutoDeleteCancelled) Settings.AutoDeleteCancelledRetentionMonths = null;
        if (!Settings.EnableAutoDeleteSnapshots) Settings.AutoDeleteSnapshotsRetentionMonths = null;
    }

    private void Validate()
    {
        if (Settings.EnableCleanup && Settings.CleanupRetentionMonths is null or <= 0)
            ModelState.AddModelError("Settings.CleanupRetentionMonths", "Set a retention of at least 1 month when cleanup is enabled.");

        if (Settings.EnableAbandonedGamesManagement && Settings.AbandonedRetentionWeeks <= 0)
            ModelState.AddModelError("Settings.AbandonedRetentionWeeks", "Set a retention of at least 1 week when abandoned-game management is enabled.");

        if (Settings.EnableAutoDeleteCancelled && Settings.AutoDeleteCancelledRetentionMonths is null or <= 0)
            ModelState.AddModelError("Settings.AutoDeleteCancelledRetentionMonths", "Set a retention of at least 1 month when auto-deleting cancelled games.");

        if (Settings.EnableAutoDeleteSnapshots && Settings.AutoDeleteSnapshotsRetentionMonths is null or <= 0)
            ModelState.AddModelError("Settings.AutoDeleteSnapshotsRetentionMonths", "Set a retention of at least 1 month when auto-deleting snapshots.");
    }
}
