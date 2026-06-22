using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Helpers;
using UltimateMonopoly.Models.DataModels;

namespace UltimateMonopoly.Services.Imports;

/// <summary>
/// Seeds the <see cref="BlockedWord"/> table from a plain-text file at config key
/// <c>BlockedWords_FilePath</c> (treated as a full path). One word/phrase per line; blank lines and
/// lines starting with '#' are ignored. Additive only — words already present (by NormalisedWord) are
/// skipped and nothing is ever deleted, so an admin's DB-side additions survive a re-seed. No-ops when
/// the config key is unset or the file is missing.
/// </summary>
public class BlockedWordImportService
{
    private readonly IConfiguration _config;
    private readonly IRepositoryManager _repos;
    private readonly ILogger<BlockedWordImportService> _logger;

    public BlockedWordImportService(IConfiguration config,
        IRepositoryManager repos,
        ILogger<BlockedWordImportService> logger)
    {
        _config = config;
        _repos = repos;
        _logger = logger;
    }

    public async Task SeedFromFileAsync()
    {
        var path = _config["BlockedWords_FilePath"];
        if (string.IsNullOrWhiteSpace(path))
            return; // not configured — nothing to seed

        if (!File.Exists(path))
        {
            _logger.LogWarning("BlockedWords_FilePath is set ({Path}) but the file was not found; skipping blocked-word seed", path);
            return;
        }

        var lines = await File.ReadAllLinesAsync(path);

        // (NormalisedWord → raw Word), de-duped on the normalised key within the file (first spelling wins).
        var fromFile = new Dictionary<string, string>();
        foreach (var raw in lines)
        {
            var word = raw.Trim();
            if (word.Length == 0 || word.StartsWith('#'))
                continue;

            var normalised = ProfanityNormaliser.Normalise(word);
            if (normalised.Length == 0)
                continue;

            fromFile.TryAdd(normalised, word);
        }

        if (fromFile.Count == 0)
            return;

        var repo = _repos.GetRepository<BlockedWord>();

        // Check against ALL rows (incl. soft-deleted): NormalisedWord is the PK, so re-adding an existing
        // key would collide. Additive only — never delete.
        var existing = await repo.AsQueryable()
            .FilterDeleted(DeletedQueryType.All).AsNoTracking()
            .Select(b => b.NormalisedWord)
            .ToListAsync();
        var existingSet = existing.ToHashSet();

        var toAdd = fromFile
            .Where(kvp => !existingSet.Contains(kvp.Key))
            .Select(kvp => new BlockedWord { NormalisedWord = kvp.Key, Word = kvp.Value })
            .ToList();

        if (toAdd.Count == 0)
            return;

        await repo.AddAsync(toAdd, IUserInfo.SYSTEM_USER_ID);
        _logger.LogInformation("Seeded {Count} new blocked word(s) from {Path}", toAdd.Count, path);
    }
}