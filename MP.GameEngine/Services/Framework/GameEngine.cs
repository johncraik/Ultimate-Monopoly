using MP.GameEngine.Abstractions;
using MP.GameEngine.Models;

namespace MP.GameEngine.Services.Framework;

public sealed class GameEngine(GameCacheModel cache, ISnapshotService snapshotService, IEngineNotifier notifier)
{
    public GameCacheModel Cache { get; } = cache;
    public IPromptProvider PromptProvider { get; } = new PromptProvider(cache, notifier);
    public ITurnStateProvider TurnStateProvider { get; } = new TurnStateProvider(cache, snapshotService);
    public IEventEmitter EventEmitter { get; } = new EventEmitter(cache);
    public IEngineNotifier Notifier { get; } = notifier;
}