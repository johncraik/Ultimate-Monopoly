using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;

/// <summary>One row in the game-details turns list (and the turn dropdown): the turn number, its current
/// player (id + resolved name), the date, whether it's the final turn, and the stored size (in characters,
/// ≈ bytes for the ASCII-dominant JSON) of this turn's snapshot and event blobs.</summary>
public class GameTurnRowViewModel
{
    public uint TurnNumber { get; }
    public string TurnId { get; }
    public string CurrentPlayerId { get; }
    public string CurrentPlayerName { get; }
    public string TurnDate { get; }
    public bool IsFinalTurn { get; }

    /// <summary>Stored size of this turn's snapshot JSON, in characters (≈ bytes). 0 if no snapshot row.</summary>
    public long SnapshotBytes { get; }
    /// <summary>Stored size of this turn's event JSON, in characters (≈ bytes). 0 if the turn emitted no events.</summary>
    public long EventsBytes { get; }
    public long TotalBytes => SnapshotBytes + EventsBytes;

    public GameTurnRowViewModel(GameTurn turn, UserViewModel? currentPlayer, long snapshotBytes, long eventsBytes)
    {
        SnapshotBytes = snapshotBytes;
        EventsBytes = eventsBytes;

        TurnNumber = turn.TurnNumber;
        TurnId = turn.Id;
        CurrentPlayerId = turn.UserId;
        CurrentPlayerName = currentPlayer == null || string.IsNullOrWhiteSpace(currentPlayer.Profile.Username)
            ? "(unknown)"
            : string.IsNullOrWhiteSpace(currentPlayer.Profile.DisplayName)
                ? currentPlayer.Profile.Username
                : currentPlayer.Profile.DisplayName;
        TurnDate = turn.TurnDateUtc.ToLocalTime().ToString("g");
        IsFinalTurn = turn.IsFinalTurn;
    }
}