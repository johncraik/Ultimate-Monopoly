namespace UltimateMonopoly.Models;

public class Board(string name, List<BoardSpace> spaces)
{
    public string Name { get; } = name;
    public List<BoardSpace> Spaces { get; } = spaces;
}