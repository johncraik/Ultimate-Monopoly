using MP.GameEngine.Enums.Games;

namespace MP.GameEngine.Models.DTOs;

public class GameDTO(string id, string name, GameRoundingRule roundingRule, GameState state, GameOutcome outcome)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public GameRoundingRule RoundingRule { get; } = roundingRule;
    public GameState State { get; } = state;
    public GameOutcome Outcome { get; } = outcome;
}