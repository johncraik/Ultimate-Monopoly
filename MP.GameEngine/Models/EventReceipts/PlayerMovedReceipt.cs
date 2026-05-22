using MP.GameEngine.Enums.Players;

namespace MP.GameEngine.Models.EventReceipts;

public class PlayerMovedReceipt : EventReceipt
{
    public ushort InitialBoardIndex { get; set; }
    public ushort FinalBoardIndex { get; set; }
    public PlayerMovementDirection Direction { get; set; }
    
    public bool IsAdvance { get; set; }
}