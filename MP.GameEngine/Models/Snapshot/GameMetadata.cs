using MP.GameEngine.Enums.Games;

namespace MP.GameEngine.Models.Snapshot;

public class GameMetadata
{
    public string GameId { get; set; }
    public string GameName { get; set; }
    public string? BoardId { get; set; }
    
    public GameRoundingRule RoundingRule { get; set; }
    
    public string CurrentTurnId { get; set; }
    public string CurrentPlayerId { get; set; }
    public uint TurnNumber { get; set; }
    
    public GameState GameState { get; set; }
    public GameOutcome? GameOutcome { get; set; }
}