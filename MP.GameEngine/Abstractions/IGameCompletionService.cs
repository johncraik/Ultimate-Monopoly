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
    
    /// <summary>
    /// Force-concludes a game as a draw outside normal gameplay. Shared by the admin "draw game"
    /// action and the abandoned-game sweep. <paramref name="isAdmin"/> controls audit attribution:
    /// <c>true</c> attributes the game/player row updates to the acting admin (the current user);
    /// <c>false</c> (the default, used by the background sweep) attributes them to the System user.
    /// </summary>
    /// <param name="engine">The game engine instance containing the game state.</param>
    /// <param name="isAdmin">True when an authenticated admin triggered the draw; false for automated/system callers.</param>
    Task<bool> TryDrawGameByAdmin(Services.Framework.GameEngine engine, bool isAdmin = false);
}