using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Models.Statistics;

public class TurnSnapshot
{
    public GameModel Game { get; set; } = new();
    public IReadOnlyList<EventReceipt> Events { get; set; } = [];
}

public class CompleteGameSnapshot
{
    public IReadOnlyList<TurnSnapshot> Turns { get; set; } = [];
    public Board Board { get; set; } = new Board("NONE", []);

    // The game's money-rounding rule — needed to value properties (mortgage value) for net worth.
    public GameRoundingRule RoundingRule { get; set; }

    public List<PlayerModel> Players => Turns.Count > 0 
        ? Turns[^1].Game.Players //Take the last turn so we can use the final state of players
        : [];
}