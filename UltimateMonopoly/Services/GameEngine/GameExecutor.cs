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

    // Idle-pump reclamation. Pumps are otherwise immortal — only finish/cancel/fault remove them —
    // so an abandoned game leaks its pump (a parked task + channel + CTS) for the life of the
    // process. The sweeper reclaims them on two thresholds:
    //   • Idle (not busy, no work for IdleThreshold): the game is dormant at a turn boundary. Drop
    //     the pump but NOT the cache — the warm working copy is preserved so a resumed game loses
    //     nothing; the cache reclaims itself via its sliding expiry (GameCacheService).
    //   • Wedged (busy, but the current item has been in flight past WedgeThreshold): a turn parked
    //     on a prompt nobody answered (a slept phone, host never stepped in). Drop the pump AND
    //     invalidate the cache, so the next access re-hydrates from the last snapshot and the lost
    //     turn is re-rolled — the automatic version of the "restart IIS to unstick it" workaround.
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan WedgeThreshold = TimeSpan.FromHours(2);

    private readonly CancellationTokenSource _sweepCts = new();
    private readonly Task _sweepTask;

    public GameExecutor(IServiceScopeFactory scopeFactory, ILogger<GameExecutor> logger,
        IHubContext<GamePlayHub> hub)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hub = hub;
        _sweepTask = Task.Run(() => SweepLoopAsync(_sweepCts.Token));
    }

    public void Enqueue(string gameId, GameWorkItem work)
    {
        while (true)
        {
            var pump = _pumps.GetOrAdd(gameId, id => new GamePump(id, _scopeFactory, _logger, OnPumpFaulted));
            switch (pump.TryEnqueue(work))
            {
                case EnqueueOutcome.Enqueued:
                    return;

                case EnqueueOutcome.Full:
                    // Backpressure (M-04): the game's queue is saturated — commands piling up behind a
                    // prompt nobody is answering, or a wedged turn. Drop this command rather than growing
                    // memory unbounded; it would be rejected by the on-pump gate re-check anyway, and the
                    // sweeper reclaims a genuinely wedged pump. The client can retry.
                    _logger.LogWarning("Game {GameId} work queue is full ({Capacity}); dropping command.",
                        gameId, GamePump.Capacity);
                    return;

                case EnqueueOutcome.Closed:
                    // The pump is faulting / shutting down and won't accept more work.
                    // Drop it and loop — GetOrAdd then spins up a fresh pump.
                    _pumps.TryRemove(new KeyValuePair<string, GamePump>(gameId, pump));
                    continue;
            }
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

    private async Task SweepLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(SweepInterval);
            while (await timer.WaitForNextTickAsync(ct))
                Sweep();
        }
        catch (OperationCanceledException)
        {
            // Shutdown — expected.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Game pump sweeper stopped unexpectedly.");
        }
    }

    private void Sweep()
    {
        var now = DateTime.UtcNow;
        foreach (var (gameId, pump) in _pumps)
        {
            try
            {
                if (!pump.IsBusy && now - pump.LastActivityUtc > IdleThreshold)
                    Reclaim(gameId, pump, invalidateCache: false, reason: "idle");
                else if (pump.IsBusy && now - pump.WorkStartedUtc > WedgeThreshold)
                    Reclaim(gameId, pump, invalidateCache: true, reason: "wedged");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reclaim pump for game {GameId}.", gameId);
            }
        }
    }

    private void Reclaim(string gameId, GamePump pump, bool invalidateCache, string reason)
    {
        // Remove the exact instance only; a concurrent fault/finish/replace leaves the new one alone.
        if (!_pumps.TryRemove(new KeyValuePair<string, GamePump>(gameId, pump)))
            return;

        _logger.LogInformation("Reclaiming {Reason} pump for game {GameId}.", reason, gameId);

        // A wedged pump's working copy may be mid-turn dirty, so force a re-hydrate from the last
        // snapshot. An idle pump sits at a turn boundary — keep the warm cache for a clean resume.
        if (invalidateCache)
            using (var scope = _scopeFactory.CreateScope())
                scope.ServiceProvider.GetRequiredService<GameCacheService>().Invalidate(gameId);

        // Fire-and-forget: DisposeAsync cancels the pump's token, which cancels any parked prompt
        // await and unwinds the work item cleanly. Don't block the sweep on it.
        _ = pump.DisposeAsync().AsTask();
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
        await _sweepCts.CancelAsync();
        try { await _sweepTask; }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        _sweepCts.Dispose();

        foreach (var pump in _pumps.Values)
            await pump.DisposeAsync();
        _pumps.Clear();
    }
}

/// <summary>Result of trying to enqueue work onto a <see cref="GamePump"/>.</summary>
internal enum EnqueueOutcome
{
    /// <summary>Accepted onto the queue.</summary>
    Enqueued,
    /// <summary>The bounded queue is full — caller should back off / drop the command.</summary>
    Full,
    /// <summary>The pump is completing (fault / shutdown) — caller should replace it.</summary>
    Closed
}

/// <summary>
/// One game's pump: a bounded channel drained by a single reader loop. The
/// loop awaits each work item to completion before dequeuing the next — so when
/// a work item parks on a prompt, the whole game's queue waits, which is exactly
/// the single-writer serialisation we want.
/// </summary>
internal sealed class GamePump : IAsyncDisposable
{
    /// <summary>Max commands that may sit QUEUED behind the in-flight one. A running item is already
    /// dequeued (not counted) and a faulted pump discards its queue, so this bounds only pending work.
    /// Legitimate turn-based play never queues this deep — a saturated queue means a parked/wedged turn.</summary>
    public const int Capacity = 10;

    private readonly string _gameId;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly Channel<GameWorkItem> _channel =
        Channel.CreateBounded<GameWorkItem>(new BoundedChannelOptions(Capacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    // Set true the moment we begin completing the channel (fault / dispose), so TryEnqueue can tell a
    // CLOSED pump (recreate a fresh one) from a merely FULL queue (reject) — TryWrite returns false for both.
    private volatile bool _closed;

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pumpTask;
    private readonly Action<string, GamePump> _onFault;

    // Liveness for the executor's idle sweeper. _lastActivityTicks bumps on enqueue and after
    // each work item completes (idle time = now − this when not busy). _workStartedTicks marks
    // when the current item began (wedge time = now − this while busy). _busy is true while a
    // work item is in flight, including while it's parked awaiting a prompt.
    private long _lastActivityTicks = DateTime.UtcNow.Ticks;
    private long _workStartedTicks;
    private volatile bool _busy;

    public bool IsBusy => _busy;
    public DateTime LastActivityUtc => new(Volatile.Read(ref _lastActivityTicks), DateTimeKind.Utc);
    public DateTime WorkStartedUtc => new(Volatile.Read(ref _workStartedTicks), DateTimeKind.Utc);

    public GamePump(string gameId, IServiceScopeFactory scopeFactory, ILogger logger,
        Action<string, GamePump> onFault)
    {
        _gameId = gameId;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _onFault = onFault;
        _pumpTask = Task.Run(() => PumpAsync(_cts.Token));
    }

    public EnqueueOutcome TryEnqueue(GameWorkItem work)
    {
        if (_closed) return EnqueueOutcome.Closed;

        if (_channel.Writer.TryWrite(work))
        {
            Volatile.Write(ref _lastActivityTicks, DateTime.UtcNow.Ticks);
            return EnqueueOutcome.Enqueued;
        }

        // TryWrite failed: either we're now closing (race with completion) or the bounded queue is full.
        return _closed ? EnqueueOutcome.Closed : EnqueueOutcome.Full;
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    _workStartedTicks = DateTime.UtcNow.Ticks;
                    _busy = true;
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
                    _closed = true;
                    _channel.Writer.TryComplete();
                    _onFault(_gameId, this);
                    break;
                }
                finally
                {
                    _busy = false;
                    Volatile.Write(ref _lastActivityTicks, DateTime.UtcNow.Ticks);
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
        _closed = true;
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