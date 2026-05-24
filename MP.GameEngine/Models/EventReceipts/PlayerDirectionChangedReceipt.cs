using MP.GameEngine.Enums.Players;

namespace MP.GameEngine.Models.EventReceipts;

public class PlayerDirectionChangedReceipt : EventReceipt
{
    public PlayerDirection InitialDirection { get; init; }
    public PlayerDirection FinalDirection { get; init; }
}