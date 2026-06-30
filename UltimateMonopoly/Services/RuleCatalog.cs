using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using MP.GameEngine.Enums;
using UltimateMonopoly.Models.ViewModels;

namespace UltimateMonopoly.Services;

public class RuleCatalog
{
    private readonly FilePathProvider _filePathProvider;
    private readonly ILogger<RuleCatalog> _logger;
    private const int RuleCacheKey = 0;
    private const string RulesFileName = "rules.json";

    private readonly ConcurrentDictionary<int, List<GameRule>> _rulesCache = new();
    
    public RuleCatalog(FilePathProvider filePathProvider,
        ILogger<RuleCatalog> logger)
    {
        _filePathProvider = filePathProvider;
        _logger = logger;
    }

    private void CacheRules(List<GameRule> rules)
    {
        _rulesCache[RuleCacheKey] = rules;
    }

    private async Task<List<GameRule>> GetRulesFromJson()
    {
        var cached = _rulesCache.TryGetValue(RuleCacheKey, out var gameRules);
        if (cached && gameRules != null) return gameRules;
        
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

        return gameRules ?? [];
    }


    public async Task<List<GameRule>> GetRules(List<RuleCode>? ruleCodes = null)
    {
        var gameRules = await GetRulesFromJson();
        if (gameRules.Count == 0)
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

    public async Task<bool> TryUpdateRules(List<GameRule> rules)
    {
        var gameRules = await GetRulesFromJson();
        if (gameRules.Count == 0)
            return false;

        foreach (var gameRule in gameRules)
        {
            var updatedRule = rules.FirstOrDefault(r => r.Section == gameRule.Section 
                                                        && r.Rule == gameRule.Rule 
                                                        && r.Point == gameRule.Point
                                                        && r.RuleCode == gameRule.RuleCode);
            if(updatedRule == null)
                continue;
            
            //Can only update title and description, and hidden state of existing rules
            gameRule.Title = updatedRule.Title;
            gameRule.RuleDescription = updatedRule.RuleDescription;
            gameRule.IsHidden = updatedRule.IsHidden;
        }
        
        CacheRules(gameRules);
        var content = JsonSerializer.Serialize(gameRules);
        
        var path = _filePathProvider.GetFilePath(FilePathProvider.FileCategory.Rules);
        await _filePathProvider.WriteFileAsync(path, RulesFileName, content);
        return true;
    }
}