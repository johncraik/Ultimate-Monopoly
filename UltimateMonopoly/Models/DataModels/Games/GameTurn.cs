using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;

namespace UltimateMonopoly.Models.DataModels.Games;

public class GameTurn : AuditModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    
    [Required]
    [MaxLength(38)]
    public string GameId { get; private set; }
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; }
    
    [Required]
    [MaxLength(38)]
    public string UserId { get; private set; }
    [ForeignKey($"{nameof(GameId)},{nameof(UserId)}")]
    public GamePlayer Player { get; set; }
    
    //This represents the final turn in the game.
    //True when everyone else is bankrupted in the previous turn, and only one player is left
    //True when a draw is declared in the previous turn (manual end game)
    //False otherwise
    //NOTE: Previous turn becasue turns are snapshotted at the begining of each turn
    public bool IsFinalTurn { get; private set; }
    
    public uint TurnNumber { get; set; }
    
    [NotMapped]
    public DateTime TurnDateUtc => CreatedUtc;
    
    public GameTurn()
    {
    }

    public GameTurn(string gameId, string userId)
    {
        GameId = gameId;
        UserId = userId;
    }

    public void SetFinalTurn() => IsFinalTurn = true;
    
    public bool IsCurrentTurn(IEnumerable<GameTurn> turns)
        => IsCurrentTurn(turns.Select(t => t.TurnNumber));

    public bool IsCurrentTurn(IEnumerable<uint> turnNumbers)
    {
        var latest = turnNumbers.Max();
        return latest == TurnNumber;
    }
}