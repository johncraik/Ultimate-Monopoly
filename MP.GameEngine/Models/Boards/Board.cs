namespace MP.GameEngine.Models.Boards;

public class Board(string name, List<BoardSpace> spaces, string? boardId = null)
{
    public string? BoardId { get; } = boardId;
    public string Name { get; } = name;
    public List<BoardSpace> Spaces { get; } = spaces;
}