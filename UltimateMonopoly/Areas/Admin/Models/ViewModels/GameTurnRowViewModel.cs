using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels;

/// <summary>One row in the game-details turns list (and the turn dropdown): the turn number, its current
/// player (id + resolved name), the date, and whether it's the final turn.</summary>
public class GameTurnRowViewModel
{
    public uint TurnNumber { get; }
    public string TurnId { get; }
    public string CurrentPlayerId { get; }
    public string CurrentPlayerName { get; }
    public string TurnDate { get; }
    public bool IsFinalTurn { get; }

    public GameTurnRowViewModel(GameTurn turn, UserViewModel? currentPlayer)
    {
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