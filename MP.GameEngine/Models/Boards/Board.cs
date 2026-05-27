namespace MP.GameEngine.Models.Boards;

public class Board(string name, List<BoardSpace> spaces, string? boardId = null)
{
    public string? BoardId { get; } = boardId;
    public string Name { get; } = name;
    public List<BoardSpace> Spaces { get; } = spaces;
    
    
    public BoardSpace GetBoardSpace(ushort index) => Spaces.FirstOrDefault(s => s.Index == index)
        ?? throw new ArgumentException($"Board space with index {index} not found");
}