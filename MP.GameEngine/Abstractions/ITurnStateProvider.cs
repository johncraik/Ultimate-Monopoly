using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Abstractions;

public interface ITurnStateProvider
{
    bool CanPortfolioCommand(string playerId, string submittingUserId);
    bool CanDeal(string playerId, string submittingUserId);
    bool CanLeaveJail(string playerId, string submittingUserId);
    bool CanEndTurn(string playerId, string submittingUserId);
    bool CanDeclareBankruptcy(string playerId, string submittingUserId);

    void TransitionToRollPhase();
    void TransitionToThirdDie();
    void TransitionToEndOfTurn();
    Task TransitionToExtraTurn(bool isTriple);
    Task TransitionToNextPlayer();
}