using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Abstractions;

public interface ITurnStateProvider
{
    bool CanPortfolioCommand(string playerId);
    bool CanDeal(string playerId);
    bool CanLeaveJail(string playerId);
    bool CanEndTurn(string playerId);
    bool CanDeclareBankruptcy(string playerId);

    void TransitionToRollPhase();
    void TransitionToThirdDie();
    void TransitionToEndOfTurn();
    GameModel TransitionToExtraTurn(bool isTriple);
    GameModel TransitionToNextPlayer();
}