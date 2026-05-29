using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using MP.GameEngine.Abstractions;
using UltimateMonopoly.Hubs;
using UltimateMonopoly.Services.Cache;
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
    private readonly IHubContext<GamePlayHub> _hub;
    private readonly ConcurrentDictionary<string, GamePump> _pumps = new();

    private const string FaultMessage =
        "An unexpected error occurred and the game cannot continue. You'll be returned to the home screen.";

    public GameExecutor(IServiceScopeFactory scopeFactory, ILogger<GameExecutor> logger,
        IHubContext<GamePlayHub> hub)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hub = hub;
    }

    public void Enqueue(string gameId, GameWorkItem work)
    {
        while (true)
        {
            var pump = _pumps.GetOrAdd(gameId, id => new GamePump(id, _scopeFactory, _logger, OnPumpFaulted));
            if (pump.TryEnqueue(work))
                return;

            // The pump is faulting / shutting down and won't accept more work.
            // Drop it and loop — GetOrAdd then spins up a fresh pump.
            _pumps.TryRemove(new KeyValuePair<string, GamePump>(gameId, pump));
        }
    }

    public async ValueTask StopAsync(string gameId)
    {
        if (_pumps.TryRemove(gameId, out var pump))
            await pump.DisposeAsync();
    }

    /// <summary>
    /// Invoked by a pump when a work item throws. Something in the game flow has
    /// gone haywire, so we abandon the whole pump (dropping any queued work — we
    /// must not keep running against a half-applied working copy) and evict the
    /// game's cache. The next <see cref="Enqueue"/> creates a fresh pump whose
    /// first work item re-hydrates the cache from the last snapshot.
    /// </summary>
    private void OnPumpFaulted(string gameId, GamePump pump)
    {
        _pumps.TryRemove(new KeyValuePair<string, GamePump>(gameId, pump));

        using (var scope = _scopeFactory.CreateScope())
            scope.ServiceProvider.GetRequiredService<GameCacheService>().Invalidate(gameId);

        // Force-quit every client in the game: show a fatal error and redirect home.
        // Fire-and-forget — recovery must not hinge on broadcast latency.
        _ = BroadcastFaultAsync(gameId);
    }

    private async Task BroadcastFaultAsync(string gameId)
    {
        try
        {
            await _hub.Clients.Group(GamePlayHub.GroupName(gameId))
                .SendAsync("GameFaulted", new GameFaultMessage(FaultMessage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast GameFaulted for game {GameId}.", gameId);
        }
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
    private readonly Action<string, GamePump> _onFault;

    public GamePump(string gameId, IServiceScopeFactory scopeFactory, ILogger logger,
        Action<string, GamePump> onFault)
    {
        _gameId = gameId;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _onFault = onFault;
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
                    // A work item threw — the game flow is haywire, so we do NOT
                    // continue with anything else queued for this game. Drop any
                    // pending work, hand off to the executor to abandon this pump and
                    // evict the (possibly dirty) cached working copy, then stop. The
                    // next enqueue spins up a fresh pump that re-hydrates from the
                    // last snapshot — the engine's recovery boundary.
                    _logger.LogError(ex, "Work item failed for game {GameId}; abandoning pump and evicting cache.", _gameId);
                    _channel.Writer.TryComplete();
                    _onFault(_gameId, this);
                    break;
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

/// <summary>Wire payload for the <c>GameFaulted</c> force-quit broadcast.</summary>
public sealed record GameFaultMessage(string Message);