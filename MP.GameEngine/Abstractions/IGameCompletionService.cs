namespace MP.GameEngine.Abstractions;

public interface IGameCompletionService
{
    /// <summary>
    /// Determines the winner of the game based on the current game state within the provided game engine.
    /// </summary>
    /// <param name="engine">The game engine instance containing the game state and other relevant information.</param>
    /// <return>A task representing the asynchronous operation to calculate and declare the winner.</return>
    Task DeclareWinner(Services.Framework.GameEngine engine);

    /// <summary>
    /// Marks the game as a draw based on the current state within the provided game engine.
    /// </summary>
    /// <param name="engine">The game engine instance containing the game's state, rules, and other related information.</param>
    /// <return>A task representing the asynchronous operation to process and finalize the game's draw state.</return>
    Task DrawGame(Services.Framework.GameEngine engine);
    
    Task<bool> TryDrawGameByAdmin(Services.Framework.GameEngine engine);
}