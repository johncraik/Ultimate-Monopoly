using Microsoft.AspNetCore.SignalR;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Prompts;
using UltimateMonopoly.Hubs;

namespace UltimateMonopoly.Services.GameEngine;

/// <summary>
/// SignalR-backed <see cref="IEngineNotifier"/>. Broadcasts prompt lifecycle
/// events to a game's in-play group (<c>game-play__{gameId}</c>). Every send is
/// fire-and-forget: the engine calls these synchronously from inside turn
/// execution and must never block on — or be broken by — a broadcast.
/// </summary>
public sealed class SignalrEngineNotifier : IEngineNotifier
{
    private readonly IHubContext<GamePlayHub> _hub;
    private readonly ILogger<SignalrEngineNotifier> _logger;

    public SignalrEngineNotifier(IHubContext<GamePlayHub> hub,
        ILogger<SignalrEngineNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public void PromptOpened(string gameId, Prompt prompt, string concurrencyStamp)
        => Send(gameId, "PromptOpened", new PromptOpenedMessage(prompt, concurrencyStamp));

    public void PromptClosed(string gameId, string promptId, string concurrencyStamp)
        => Send(gameId, "PromptClosed", new PromptClosedMessage(promptId, concurrencyStamp));

    public void StateChanged(GameCacheModel cache)
        // Ships the whole cache (Board + Events are [JsonIgnore]d); ConcurrencyStamp is the version.
        => Send(cache.GameId, "StateChanged", cache);

    public void GameCompleted(string gameId)
        => Send(gameId, "GameCompleted", new GameCompletedMessage(gameId));

    private void Send<T>(string gameId, string method, T message)
    {
        // Fire-and-forget: never await broadcast latency inside engine flow.
        _ = SendAsync(gameId, method, message);
    }

    private async Task SendAsync<T>(string gameId, string method, T message)
    {
        try
        {
            await _hub.Clients.Group(GamePlayHub.GroupName(gameId)).SendAsync(method, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast {Method} for game {GameId}.", method, gameId);
        }
    }
}

/// <summary>
/// Wire payload for <c>PromptOpened</c>. <see cref="Prompt"/> is deliberately
/// declared as the polymorphic base type so System.Text.Json emits the
/// <c>[JsonPolymorphic]</c> discriminator and the client can deserialise the
/// concrete prompt type.
/// </summary>
public sealed record PromptOpenedMessage(Prompt Prompt, string ConcurrencyStamp);

/// <summary>Wire payload for <c>PromptClosed</c>.</summary>
public sealed record PromptClosedMessage(string PromptId, string ConcurrencyStamp);

/// <summary>Wire payload for <c>GameCompleted</c> — the in-game pages redirect to the finished-game page.</summary>
public sealed record GameCompletedMessage(string GameId);