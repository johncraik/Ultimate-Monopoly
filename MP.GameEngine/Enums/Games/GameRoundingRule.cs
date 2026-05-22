using System.ComponentModel;

namespace MP.GameEngine.Enums.Games;

public enum GameRoundingRule
{
    [Description("No rounding applied")]
    None,
    
    [Description("Round money to the nearest 5")]
    To5,
    
    [Description("Round money to the nearest 10")]
    To10,
    
    [Description("Round money to the nearest 20")]
    To20,
    
    [Description("Round money to the nearest 50")]
    To50
}