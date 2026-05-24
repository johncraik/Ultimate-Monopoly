using MP.GameEngine.Enums.Players;

namespace MP.GameEngine.Models.EventReceipts;

public class PlayerMovedReceipt : EventReceipt
{
    public ushort InitialBoardIndex { get; init; }
    public ushort FinalBoardIndex { get; init; }
    public PlayerMovementDirection Direction { get; init; }
    
    public bool IsAdvance { get; init; }
}