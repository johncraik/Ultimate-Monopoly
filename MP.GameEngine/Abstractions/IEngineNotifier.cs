using MP.GameEngine.Models;
using MP.GameEngine.Models.Prompts;

namespace MP.GameEngine.Abstractions;

/// <summary>
/// Web-implemented seam the engine uses to announce prompt lifecycle changes to
/// the outside world (typically a SignalR broadcast). Mirrors the
/// contract-in-engine / impl-in-web layering of <see cref="ISnapshotService"/> —
/// the engine knows "a prompt opened / closed", not how it reaches a client. See
/// <c>design-docs/choice-events.md</c> §11.
/// </summary>
/// <remarks>
/// Implementations MUST be fire-and-forget. The engine calls these synchronously
/// from inside turn execution and never awaits them, so a broadcast's latency or
/// failure can neither stall nor break the turn. The
/// <paramref name="concurrencyStamp">concurrency stamp</paramref> is passed so a
/// client can reconcile the notification against the cache state it last read.
/// </remarks>
public interface IEngineNotifier
{
    /// <summary>A prompt has just become the cache's pending prompt.</summary>
    void PromptOpened(string gameId, Prompt prompt, string concurrencyStamp);

    /// <summary>The pending prompt has been resolved or cancelled and cleared.</summary>
    void PromptClosed(string gameId, string promptId, string concurrencyStamp);

    /// <summary>
    /// The game's live state changed. Implementations broadcast a projection of
    /// the whole <paramref name="cache"/> (board and per-turn receipts are
    /// excluded via <c>[JsonIgnore]</c>) so connected clients can re-render.
    /// Fired at key points — when a prompt opens (the game pauses) and when a
    /// unit of engine work completes.
    /// </summary>
    void StateChanged(GameCacheModel cache);

    /// <summary>
    /// The game has finished (a winner or a draw). Implementations announce it to
    /// the game's connected clients so they can move on (the in-game pages redirect
    /// to the finished-game results). Called once the conclusion is persisted.
    /// </summary>
    void GameCompleted(string gameId);
    /// <summary>
    /// Host-initiated: tells the game's connected clients to hard-reload and re-fetch the
    /// current live state — the host "Force Refresh" control (<c>Game-UI.md</c>). Not an
    /// engine event; the web layer calls it directly, off the turn pump, so it fires even
    /// while the pump is parked on a prompt.
    /// </summary>
    void ForceRefresh(string gameId);

    /// <summary>
    /// Host-initiated: the game was cancelled (abandoned — no winner/draw). Tells the game's
    /// connected clients to leave (the in-game pages redirect home). Not an engine event;
    /// the web layer calls it after the cancellation is committed.
    /// </summary>
    void GameCancelled(string gameId);
}