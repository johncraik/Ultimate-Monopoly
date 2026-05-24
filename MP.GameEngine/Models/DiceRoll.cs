using System.ComponentModel.DataAnnotations;

namespace MP.GameEngine.Models;

public class DiceRoll
{
    [Range(1, 6)]
    public ushort Die1 { get; }
    
    [Range(1, 6)]
    public ushort? Dice2 { get; }
    [Range(1, 6)]
    public ushort? ThirdDie { get; }
    
    public bool IsTurnRoll { get; }

    public DiceRoll(ushort die1, ushort die2, ushort thirdDie, bool isTurnRoll = true)
    {
        Die1 = die1;
        Dice2 = die2;
        ThirdDie = thirdDie;
        IsTurnRoll = isTurnRoll;
    }

    public DiceRoll(ushort die1, ushort? die2 = null)
    {
        Die1 = die1;
        Dice2 = die2;
        
        ThirdDie = null;
        IsTurnRoll = false;
    }
}