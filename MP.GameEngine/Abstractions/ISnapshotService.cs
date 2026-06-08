using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Abstractions;

public interface ISnapshotService
{
    /// <summary>
    /// Persists a new turn record (<c>GameTurn</c>) and its game-state
    /// snapshot (<c>GameSnapshot</c>) for the supplied game. A new turn
    /// id is generated as part of this call and **written back to
    /// <paramref name="game"/>.<c>Metadata.CurrentTurnId</c>** so the
    /// in-memory cache and the database agree on the current turn after
    /// the call returns — callers should expect <c>game.Metadata</c> to
    /// be updated. Throws on persistence failure.
    /// </summary>
    Task CreateSnapshotAsync(GameModel game, bool completeTransaction = true, bool finalTurn = false);

    Task CreateTurnEventSnapshotAsync(string gameId, string turnId, List<EventReceipt> receipts);
}