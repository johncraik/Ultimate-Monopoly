using System.Collections.Concurrent;
using System.Threading.Channels;
using MP.GameEngine.Abstractions;
using EngineRuntime = MP.GameEngine.Services.Framework.GameEngine;

namespace UltimateMonopoly.Services.GameEngine;

/// <summary>
/// A unit of work run against a game's engine on its dedicated pump. Receives
/// the resolved <see cref="EngineRuntime"/> for the game, the scoped service
/// provider for this run (resolve rule services / orchestrators from here), and
/// the pump's cancellation token.
/// </summary>
public delegate Task GameWorkItem(EngineRuntime engine, IServiceProvider services, CancellationToken ct);

/// <summary>
/// Serialises all engine work for a game onto a single background pump, so the
/// cache's working-copy model has exactly one writer per game. Hub methods
/// enqueue and return; the work runs off the connection thread. This is the
/// structural fix for the <c>SubmitPrompt</c> deadlock (the submission path
/// stays out-of-band and never queues behind an in-flight, prompt-blocked
/// command). See <c>design-docs</c> session notes (per-game executor).
/// </summary>
public interface IGameExecutor
{
    /// <summary>
    /// Queues <paramref name="work"/> onto the single-writer pump for
    /// <paramref name="gameId"/>. Returns immediately — the item runs in the
    /// background, one at a time per game, in enqueue order.
    /// </summary>
    void Enqueue(string gameId, GameWorkItem work);

    /// <summary>Stops and disposes a game's pump (e.g. when the game finishes).</summary>
    ValueTask StopAsync(string gameId);
}

public sealed class GameExecutor : IGameExecutor, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameExecutor> _logger;
    private readonly ConcurrentDictionary<string, GamePump> _pumps = new();

    public GameExecutor(IServiceScopeFactory scopeFactory, ILogger<GameExecutor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(string gameId, GameWorkItem work)
    {
        var pump = _pumps.GetOrAdd(gameId, id => new GamePump(id, _scopeFactory, _logger));
        if (!pump.TryEnqueue(work))
            _logger.LogWarning("Dropped work item for game {GameId}: pump is shutting down.", gameId);
    }

    public async ValueTask StopAsync(string gameId)
    {
        if (_pumps.TryRemove(gameId, out var pump))
            await pump.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var pump in _pumps.Values)
            await pump.DisposeAsync();
        _pumps.Clear();
    }
}

/// <summary>
/// One game's pump: an unbounded channel drained by a single reader loop. The
/// loop awaits each work item to completion before dequeuing the next — so when
/// a work item parks on a prompt, the whole game's queue waits, which is exactly
/// the single-writer serialisation we want.
/// </summary>
internal sealed class GamePump : IAsyncDisposable
{
    private readonly string _gameId;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly Channel<GameWorkItem> _channel =
        Channel.CreateUnbounded<GameWorkItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pumpTask;

    public GamePump(string gameId, IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _gameId = gameId;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pumpTask = Task.Run(() => PumpAsync(_cts.Token));
    }

    public bool TryEnqueue(GameWorkItem work) => _channel.Writer.TryWrite(work);

    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await RunAsync(work, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A turn threw. Per the engine policy the recovery boundary is
                    // the last snapshot — the web layer re-hydrates from it. The
                    // pump survives so the game can continue once re-hydrated.
                    // TODO: evict the cache entry here so the dirty working copy is
                    // discarded and the next item re-hydrates from the snapshot.
                    _logger.LogError(ex, "Work item failed for game {GameId}.", _gameId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Pump shutdown — expected.
        }
    }

    private async Task RunAsync(GameWorkItem work, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IGameEngineFactory>();
        var engine = await factory.GetAsync(_gameId);
        await work(engine, scope.ServiceProvider, ct);

        // Work item completed successfully — push the resulting state to clients.
        // (On a throw we never reach here; the catch logs and the snapshot is the
        // recovery boundary. Mid-work prompt pauses push their own state from the
        // prompt seam.)
        engine.Notifier.StateChanged(engine.Cache);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync();
        try
        {
            await _pumpTask;
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping pump for game {GameId}.", _gameId);
        }
        _cts.Dispose();
    }
}