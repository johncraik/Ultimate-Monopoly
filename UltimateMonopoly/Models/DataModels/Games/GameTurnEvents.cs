using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;

namespace UltimateMonopoly.Models.DataModels.Games;

public class GameTurnEvents : AuditModel
{
    [Key]
    [ForeignKey(nameof(GameTurn))]
    public string TurnId { get; set; }
    [ForeignKey(nameof(TurnId))]
    public GameTurn GameTurn { get; set; }
    
    [Required]
    [MaxLength(38)]
    public string GameId { get; set; }
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; }
    
    [Required]
    public string EventsJson { get; set; }

    public GameTurnEvents()
    {
    }

    public GameTurnEvents(string turnId, string gameId, string eventsJson)
    {
        TurnId = turnId;
        GameId = gameId;
        EventsJson = eventsJson;
    }
}