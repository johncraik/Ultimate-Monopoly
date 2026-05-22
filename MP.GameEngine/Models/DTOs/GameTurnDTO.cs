namespace MP.GameEngine.Models.DTOs;

public class GameTurnDTO(string id, string playerId, uint turnNumber)
{
    public string Id { get; } = id;
    public string PlayerId { get; } = playerId;
    public uint TurnNumber { get; } = turnNumber;
}