using System.Text.Json;
using UltimateMonopoly.Areas.Admin.Models;
using UltimateMonopoly.Services;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Singleton holder for the admin-configurable <see cref="GameSettings"/> (C1 — Game Settings). Reads from
/// and writes to <c>config/rules/settings.json</c> via the shared <see cref="FilePathProvider"/>, mirroring
/// <c>TurnTaxService</c>. <see cref="Get"/> always hands out a copy so callers can never mutate live state.
/// <para>The background jobs these settings drive (cleanup / abandoned-game management / auto-delete) are a
/// deferred phase — the settings persist here and wait for the jobs to consume them.</para>
/// </summary>
public class SettingsDictionary
{
    private readonly FilePathProvider _filePathProvider;
    private const string _settingsFileName = "settings.json";

    private GameSettings _settings = new();

    public SettingsDictionary(FilePathProvider filePathProvider)
    {
        _filePathProvider = filePathProvider;
    }

    /// <summary>A copy of the current settings — the admin editor mutates the copy, never live state.</summary>
    public GameSettings Get() => Clone(_settings);

    /// <summary>Loads settings from <c>settings.json</c> at startup; keeps the defaults when the file is absent.</summary>
    public async Task Import()
    {
        var path = Path.Combine(_filePathProvider.GetFilePath(FilePathProvider.FileCategory.Rules), _settingsFileName);
        if (!File.Exists(path)) return;

        var txt = await _filePathProvider.ReadFileAsync(path);
        var settings = JsonSerializer.Deserialize<GameSettings>(txt);
        if (settings == null) return;

        _settings = settings;
    }

    /// <summary>Persists new settings to <c>settings.json</c> and refreshes the in-memory copy.</summary>
    public async Task Update(GameSettings settings)
    {
        _settings = Clone(settings);

        var content = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        var path = _filePathProvider.GetFilePath(FilePathProvider.FileCategory.Rules);
        await _filePathProvider.WriteFileAsync(path, _settingsFileName, content);
    }

    private static GameSettings Clone(GameSettings s) => new()
    {
        EnableCleanup = s.EnableCleanup,
        CleanupRetentionMonths = s.CleanupRetentionMonths,
        EnableAbandonedGamesManagement = s.EnableAbandonedGamesManagement,
        AbandonedGameAction = s.AbandonedGameAction,
        AbandonedRetentionWeeks = s.AbandonedRetentionWeeks,
        EnableAutoDeleteCancelled = s.EnableAutoDeleteCancelled,
        AutoDeleteCancelledRetentionMonths = s.AutoDeleteCancelledRetentionMonths,
        EnableAutoDeleteSnapshots = s.EnableAutoDeleteSnapshots,
        AutoDeleteSnapshotsRetentionMonths = s.AutoDeleteSnapshotsRetentionMonths
    };
}
