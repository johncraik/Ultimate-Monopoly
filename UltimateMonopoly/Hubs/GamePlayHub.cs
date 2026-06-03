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
    private readonly GameService _gameService;
    private readonly PlayerProfileService _playerProfiles;

    public GamePlayHub(GameService gameService, IGameEngineFactory engineFactory,
        PlayerProfileService playerProfiles)
        : base(gameService)
    {
        _engineFactory = engineFactory;
        _gameService = gameService;
        _playerProfiles = playerProfiles;
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

    /// <summary>
    /// Starts the current player's turn (kicks off the orchestrator, which opens
    /// the dice prompt). Allowed at StartOfTurn (<c>CanStartTurn</c>); enqueues on
    /// the game's single-writer executor. Returns false when not allowed or the
    /// engine is unavailable.
    /// </summary>
    public async Task<bool> StartTurn()
    {
        var gameId = GetGameId();
        if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(Context.UserIdentifier))
            return false;

        var engine = await _engineFactory.GetAsync(gameId);
        var current = engine.Cache.Game.CurrentPlayer();
        if (current is null) return false;
        
        if (!engine.TurnStateProvider.CanStartTurn(current.PlayerId, Context.UserIdentifier))
            return false;

        _gameService.EnqueueTurn(gameId, Context.UserIdentifier);
        return true;
    }

    /// <summary>
    /// Ends the current player's turn. Gates on the host-bypass-aware
    /// <c>CanEndTurn</c> capability as an early-out, then enqueues the work on
    /// the game's single-writer executor (the pump remains the authoritative
    /// writer). Returns false when not allowed or the engine is unavailable.
    /// </summary>
    public async Task<bool> EndTurn()
    {
        var gameId = GetGameId();
        if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(Context.UserIdentifier))
            return false;

        var engine = await _engineFactory.GetAsync(gameId);
        var current = engine.Cache.Game.CurrentPlayer();
        if (current is null) return false;

        if (!engine.TurnStateProvider.CanEndTurn(current.PlayerId, Context.UserIdentifier))
            return false;

        _gameService.EnqueueEndTurn(gameId, Context.UserIdentifier);
        return true;
    }


    // ─── Portfolio commands ──────────────────────────────────────────────
    // Player-initiated property actions on the named property (boardIndex). Each
    // gates on the host-bypass-aware CanPortfolioCommand as an early-out, then
    // hands to PlayerProfileService to enqueue on the single-writer executor
    // (which re-checks authoritatively). The engine command opens its
    // AcquirePropertyPrompt confirmation, which returns over this same connection.

    public Task<bool> MortgageProperty(ushort boardIndex)
        => RunPortfolioCommand(boardIndex, _playerProfiles.EnqueueMortgage);

    public Task<bool> UnmortgageProperty(ushort boardIndex)
        => RunPortfolioCommand(boardIndex, _playerProfiles.EnqueueUnmortgage);

    public Task<bool> UnReserveProperty(ushort boardIndex)
        => RunPortfolioCommand(boardIndex, _playerProfiles.EnqueueUnReserve);

    private async Task<bool> RunPortfolioCommand(ushort boardIndex, Action<string, string, ushort> enqueue)
    {
        var gameId = GetGameId();
        if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(Context.UserIdentifier))
            return false;

        var engine = await _engineFactory.GetAsync(gameId);
        var current = engine.Cache.Game.CurrentPlayer();
        if (current is null) return false;

        // Optimistic pre-check; the enqueued work item re-checks authoritatively on the pump.
        if (!engine.TurnStateProvider.CanPortfolioCommand(current.PlayerId, Context.UserIdentifier))
            return false;

        enqueue(gameId, Context.UserIdentifier, boardIndex);
        return true;
    }
}