using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Models;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Framework;

public sealed class GameEngine(GameCacheModel cache, 
    ISnapshotService snapshotService, 
    IEngineNotifier notifier, 
    IShortfallService shortfallService)
{
    public GameCacheModel Cache { get; } = cache;
    public IPromptProvider PromptProvider { get; } = new PromptProvider(cache, notifier);
    public ITurnStateProvider TurnStateProvider { get; } = new TurnStateProvider(cache, snapshotService);
    public IEventEmitter EventEmitter { get; } = new EventEmitter(cache);
    public IEngineNotifier Notifier { get; } = notifier;
    
    public IShortfallService ShortfallService { get; } = shortfallService;

    /// <summary>
    /// Adds the provided rule code to the game engine's cache.
    /// </summary>
    /// <param name="code">The rule code to be added to the game cache.</param>
    internal void CiteRule(RuleCode code)
    {
        //No-op if the code has already been cited.
        if(Cache.RuleCodes.Contains(code))
            return;
            
        Cache.AddRuleCode(code);
    }
}