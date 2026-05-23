using MP.GameEngine.Enums.Games;

namespace MP.GameEngine.Models.DTOs;

public class GameDTO(string id, string name, string boardId, GameRoundingRule roundingRule, string hostPlayerId, GameState state, GameOutcome outcome)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string BoardId { get; } = boardId;
    public string HostPlayerId { get; } = hostPlayerId;
    public GameRoundingRule RoundingRule { get; } = roundingRule;
    public GameState State { get; } = state;
    public GameOutcome Outcome { get; } = outcome;
}