namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;

/// <summary>Backs the admin game-details page: the game (info + host + players, via <see cref="GameViewModel"/>)
/// plus its turns (newest first), for the turns list and the read-only state drawer.</summary>
public class GameDetailViewModel
{
    public GameViewModel Game { get; }
    public IReadOnlyList<GameTurnRowViewModel> Turns { get; }

    // Whole-game stored sizes = the sum of the listed turns, so "Total Game" always matches the turns shown.
    public long GameSnapshotBytes => Turns.Sum(t => t.SnapshotBytes);
    public long GameEventsBytes => Turns.Sum(t => t.EventsBytes);
    public long GameTotalBytes => GameSnapshotBytes + GameEventsBytes;

    public GameDetailViewModel(GameViewModel game, IReadOnlyList<GameTurnRowViewModel> turns)
    {
        Game = game;
        Turns = turns;
    }
}