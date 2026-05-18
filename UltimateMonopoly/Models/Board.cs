namespace UltimateMonopoly.Models;

public class Board
{
    public string Name { get; }
    public List<BoardSpace> Spaces { get; }

    public Board(List<BoardSpace> spaces)
    {
        Name = "Monopoly Board";
        Spaces = spaces;
    }

    public Board(string name, List<BoardSpace> spaces)
    {
        Name = name;
        Spaces = spaces;
    }
}