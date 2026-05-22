using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models.Boards;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Services.Cache;

namespace UltimateMonopoly.Models.DataModels.Games;

[Index(nameof(CreatedById), nameof(State))]
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
    
    [Required]
    [MaxLength(_joinCodeLength)]
    public string JoinCode { get; private set; }

    [NotMapped] 
    public const string JoinCodePrefix = "MP";
    [NotMapped]
    private const int _joinCodeLength = 7;
    
    [MaxLength(128)]
    public string Name { get; private set; }
    
    public GameState State { get; private set; }
    public GameOutcome Outcome { get; private set; }
    public GameRoundingRule RoundingRule { get; private set; }
    
    public ICollection<GamePlayer> Players { get; set; } 
    public ICollection<GameTurn> Turns { get; set; }
    public ICollection<GameSnapshot> Snapshots { get; set; }
    

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

    public void SetJoinCode(string code)
    {
        if(!string.IsNullOrWhiteSpace(JoinCode))
            return;
        
        JoinCode = JoinCodePrefix + code;
    }

    public async Task SetBoard(BoardCacheService cache)
    {
        var boards = await cache.GetAllBoards();
        var board = boards.FirstOrDefault(b => b.BoardId == BoardId);
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