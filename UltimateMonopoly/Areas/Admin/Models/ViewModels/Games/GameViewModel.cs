using JC.Core.Extensions;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;

public class GameViewModel
{
    public string GameId { get; }
    public string TurnId { get; }
    public string? SkinId { get; }
    
    public string HostUserId { get; }
    public uint TurnNumber { get; }
    public string BoardName { get; }

    public string Name { get; }
    public string JoinCode { get; }
    
    public GameState State { get; }
    public string StateDisplay { get; }
    
    public GameOutcome Outcome { get; }
    public string OutcomeDisplay { get; }
    
    public GameRoundingRule RoundingRule { get; }
    public string RoundingRuleDisplay { get; }
    
    public UserViewModel HostUser { get; }
    public IReadOnlyList<UserViewModel> Players { get; }

    public GameViewModel(UltimateMonopoly.Models.DataModels.Games.Game game, GameTurn turn, 
        UserViewModel? host, IEnumerable<UserViewModel> players, string? defaultBoardName = null)
    {
        GameId = game.Id;
        TurnId = turn.Id;
        SkinId = game.BoardId;

        HostUserId = game.UserId;
        TurnNumber = turn.TurnNumber;
        BoardName = game.BoardSkin?.Name ?? defaultBoardName ?? "Monopoly Board";

        Name = game.Name;
        JoinCode = game.JoinCode;
        
        State = game.State;
        StateDisplay = game.State.ToDisplayName();
        
        Outcome = game.Outcome;
        OutcomeDisplay = game.Outcome.ToDisplayName();
        
        RoundingRule = game.RoundingRule;
        RoundingRuleDisplay = game.RoundingRule.GetDescription();
        
        HostUser = host ?? new UserViewModel();
        Players = players.ToList().AsReadOnly();
    }
}