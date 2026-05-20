

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UltimateMonopoly.Models.DataModels.Games;

public class GameSnapshot
{
    [Key]
    [MaxLength(38)]
    public string TurnId { get; private set; }
    [ForeignKey(nameof(TurnId))]
    public GameTurn Turn { get; set; }
    
    [Required]
    [MaxLength(38)]
    public string GameId { get; private set; }
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; }
    
    [Required]
    public string StateJson { get; set; }

    public GameSnapshot()
    {
    }

    public GameSnapshot(string turnId, string gameId)
    {
        TurnId = turnId;
        GameId = gameId;
    }
}