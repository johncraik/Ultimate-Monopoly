namespace MP.GameEngine.Models.DTOs;

public class PlayerDTO(string id, ushort orderId, ushort dice1, ushort dice2)
{
    public string Id { get; } = id;
    public ushort OrderId { get; } = orderId;
    public ushort Dice1 { get; } = dice1;
    public ushort Dice2 { get; } = dice2;
}