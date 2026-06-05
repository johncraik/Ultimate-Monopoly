using System.Collections.Concurrent;
using System.Text.Json;
using MP.GameEngine.Enums;
using UltimateMonopoly.Models.ViewModels;

namespace UltimateMonopoly.Services;

public class RuleCatalog
{
    private readonly FilePathProvider _filePathProvider;
    private readonly ILogger<RuleCatalog> _logger;
    private const string RulesFileName = "rules.json";

    private readonly ConcurrentDictionary<DateTime, List<GameRule>> _rulesCache = new();
    
    public RuleCatalog(FilePathProvider filePathProvider,
        ILogger<RuleCatalog> logger)
    {
        _filePathProvider = filePathProvider;
        _logger = logger;
    }

    private void CacheRules(List<GameRule> rules)
    {
        _rulesCache[DateTime.UtcNow.Date] = rules;
    }
    
    public async Task<List<GameRule>> GetRules(List<RuleCode>? ruleCodes = null)
    {
        var cached = _rulesCache.TryGetValue(DateTime.UtcNow.Date, out var gameRules);
        if (!cached)
        {
            var path = _filePathProvider.GetFilePath(FilePathProvider.FileCategory.Rules);
            path = Path.Combine(path, RulesFileName);

            if (!File.Exists(path))
                return [];

            try
            {
                var fileTxt = await _filePathProvider.ReadFileAsync(path);
                gameRules = JsonSerializer.Deserialize<List<GameRule>>(fileTxt);
                if (gameRules is { Count: > 0 })
                    CacheRules(gameRules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to read game rules");
                return [];
            }
        }

        if (gameRules == null || gameRules.Count == 0)
            return [];
        
        if(ruleCodes == null)
            return gameRules.OrderBy(r => r.Section)
                .ThenBy(r => r.Rule)
                .ThenBy(r => r.Point)
                .ToList();
        
        //Returns a list of matched game rules to the rule codes
        //Retains the order of the rule codes:
        return ruleCodes.Select(ruleCode => 
            gameRules.FirstOrDefault(r => r.RuleCode != null && r.RuleCode == ruleCode))
            .OfType<GameRule>()
            .ToList();
    }
}