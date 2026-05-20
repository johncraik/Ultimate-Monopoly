using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using UltimateMonopoly.Enums.Games;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Services.GameConfig;

namespace UltimateMonopoly.Models.DataModels.Games;

public class Game : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    
    [NotMapped]
    public string UserId => CreatedById ?? throw new InvalidOperationException("User id not set");
    
    //Null board ID => default board
    [MaxLength(38)]
    public string? BoardId { get; private set; }
    [ForeignKey(nameof(BoardId))]
    public BoardSkin? BoardSkin { get; set; }
    
    [NotMapped]
    public Board? GameBoard { get; private set; }
    
    [MaxLength(128)]
    public string Name { get; private set; }
    
    public GameState State { get; private set; }
    public GameOutcome Outcome { get; private set; }
    public GameRoundingRule RoundingRule { get; private set; }
    
    public List<GamePlayer> Players { get; set; } 
    

    //EF Core constructor
    public Game()
    {
    }

    //Create new game constructor
    public Game(string? name = null, string? boardSkinId = null, GameRoundingRule roundingRule = GameRoundingRule.None)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = $"Game: {DateTime.UtcNow.ToLocalTime():dd-MMMM yyyy} - [{DateTime.UtcNow.ToLocalTime():HH:mm}]";
        
        Name = name;
        BoardId = boardSkinId;
        
        State = GameState.Setup;
        Outcome = GameOutcome.None;
        RoundingRule = roundingRule;
    }

    public async Task SetBoard(BoardCacheService cache)
    {
        var boards = await cache.GetAllBoards();
        var board = boards.FirstOrDefault(b => b.SkinId == BoardId);
        if(!string.IsNullOrEmpty(BoardId) && board == null)
            throw new InvalidOperationException("Board not found");
        
        GameBoard = board;
    }

    public bool StartGame()
    {
        if(State != GameState.Setup)
            return false;
        
        State = GameState.InPlay;
        return true;
    }

    public bool CancelGame()
    {
        if(State == GameState.Finished || State == GameState.Cancelled)
            return false;
        
        State = GameState.Cancelled;
        return true;
    }
    
    public bool EndGame(GameOutcome outcome)
    {
        if(State != GameState.InPlay)
            return false;
        
        if(outcome == GameOutcome.None)
            throw new InvalidOperationException("Outcome cannot be None");
        
        State = GameState.Finished;
        Outcome = outcome;
        return true;
    }
}