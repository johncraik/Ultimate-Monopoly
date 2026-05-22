namespace MP.GameEngine.Models.EventReceipts;

public class PlayerLeftJailReceipt : EventReceipt
{
    public ushort BoardIndex { get; set; }
    public uint JailCost { get; set; }
}