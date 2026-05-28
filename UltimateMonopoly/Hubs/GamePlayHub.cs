using JC.Core.Models;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Prompts;
using UltimateMonopoly.Services.GameEngine;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Hubs;

public class GamePlayHub : GameBaseHub
{
    private const string Prefix = "game-play";

    private readonly IGameEngineFactory _engineFactory;

    public GamePlayHub(GameService gameService, IGameEngineFactory engineFactory)
        : base(gameService)
    {
        _engineFactory = engineFactory;
    }

    protected override string GroupPrefix => Prefix;

    public static string GroupName(string gameId) => GroupName(Prefix, gameId);

    /// <summary>
    /// Submits a response to the game's open prompt. Out-of-band by design — it
    /// resolves the pending prompt's awaiter directly rather than queueing on
    /// the executor, so it unblocks the (prompt-parked) in-flight work item
    /// instead of deadlocking behind it. Returns false when stale or invalid;
    /// the client should refresh and re-render.
    /// </summary>
    public async Task<bool> SubmitPrompt(string concurrencyStamp, PromptResponse response)
    {
        var gameId = GetGameId();
        if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(Context.UserIdentifier))
            return false;

        var engine = await _engineFactory.GetAsync(gameId);
        return engine.PromptProvider.TrySubmit(Context.UserIdentifier, concurrencyStamp, response);
    }

    /// <summary>
    /// Returns the currently open prompt plus the cache's concurrency stamp for
    /// a connecting / reconnecting client, or null when the engine is idle.
    /// Covers the prompt opening before this client joined the group — e.g. the
    /// first turn's dice prompt opens at game start, before phones navigate in.
    /// </summary>
    public async Task<PromptOpenedMessage?> GetCurrentPrompt()
    {
        var gameId = GetGameId();
        if (string.IsNullOrEmpty(gameId)) return null;

        var engine = await _engineFactory.GetAsync(gameId);
        var pending = engine.Cache.PendingPrompt;
        return pending is null
            ? null
            : new PromptOpenedMessage(pending.Prompt, engine.Cache.ConcurrencyStamp);
    }

    /// <summary>
    /// Returns the game's board layout for a connecting client to render. The
    /// board is static for the game's lifetime and is excluded from the live
    /// state broadcast, so clients fetch it once here rather than receiving it on
    /// every frame.
    /// </summary>
    public async Task<Board?> GetBoard()
    {
        var gameId = GetGameId();
        if (string.IsNullOrEmpty(gameId)) return null;

        var engine = await _engineFactory.GetAsync(gameId);
        return engine.Cache.Board;
    }
}