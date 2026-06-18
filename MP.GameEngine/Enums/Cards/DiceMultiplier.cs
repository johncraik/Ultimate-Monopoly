namespace MP.GameEngine.Enums.Cards;

/// <summary>Scales a card's amount by a roll (e.g. "£200 times 1 die", "× 2 dice").</summary>
public enum DiceMultiplier
{
    None,
    OneDie,
    TwoDice,
    /// <summary>A fresh two-dice total multiplied by the third die already rolled this turn ("roll 2 dice × the third die").</summary>
    TwoDiceByThirdDie
}