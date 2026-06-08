namespace UltimateMonopoly.Enums;

public enum PlayerGameOutcome
{
    /// <summary>Last non-bankrupt player in the game</summary>
    Winner,
    
    /// <summary>Drawn game, and player was not bankrupted in the game</summary>
    Drawn,
    
    /// <summary>Drawn or won game, but player was bankrupted in the game</summary>
    Loser
}