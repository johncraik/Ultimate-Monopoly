using MP.GameEngine.Abstractions;
using MP.GameEngine.Models;
using MP.GameEngine.Models.EventReceipts;

namespace MP.GameEngine.Services.Framework;

/// <summary>
/// Default <see cref="IEventEmitter"/> implementation. Forwards to the
/// cache's <see cref="GameCacheModel.AddEvent"/>, which assigns
/// <see cref="EventReceipt.TurnNumber"/> and
/// <see cref="EventReceipt.SequenceIndex"/> and re-stamps the concurrency
/// stamp. One instance per game cache.
/// </summary>
public sealed class EventEmitter : IEventEmitter
{
    private readonly GameCacheModel _cache;

    public EventEmitter(GameCacheModel cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public void Emit(EventReceipt receipt) => _cache.AddEvent(receipt);
}
