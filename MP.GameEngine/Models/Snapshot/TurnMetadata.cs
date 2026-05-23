using MP.GameEngine.Enums.Games;

namespace MP.GameEngine.Models.Snapshot;

public class TurnMetadata
{
    public string CurrentTurnId { get; set; }
    public string CurrentPlayerId { get; set; }
    public uint TurnNumber { get; set; }

    public TurnMetadata()
    {
    }

    public TurnMetadata(TurnMetadata metadata)
    {
        CurrentTurnId = metadata.CurrentTurnId;
        CurrentPlayerId = metadata.CurrentPlayerId;
        TurnNumber = metadata.TurnNumber;
    }
}