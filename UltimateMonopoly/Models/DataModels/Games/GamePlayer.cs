using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JC.Core.Models.Auditing;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Helpers.RuleSet;
using UltimateMonopoly.Enums;

namespace UltimateMonopoly.Models.DataModels.Games;

[PrimaryKey(nameof(GameId), nameof(UserId))]
[Index(nameof(GameId), nameof(Dice1), nameof(Dice2), IsUnique = true)]
[Index(nameof(UserId))]
public class GamePlayer : AuditModel
{
    [Required]
    [MaxLength(38)]
    public string GameId { get; set; }
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; }
    
    [Required]
    [MaxLength(38)]
    public string UserId { get; set; }
    
    //Max of 8 players
    [Range(0, 7)]
    public ushort OrderId { get; private set; }
    
    [Range(1, 6)]
    public ushort? Dice1 { get; private set; }
    [Range(1, 6)]
    public ushort? Dice2 { get; private set; }
    
    public PlayerGameOutcome? PlayerGameOutcome { get; set; }

    public GamePlayer()
    {
    }

    public GamePlayer(string gameId, string userId)
    {
        GameId = gameId;
        UserId = userId;
    }

    public bool SetOrderId(ushort orderId)
    {
        if(orderId > RuleDictionary.MaximumPlayers - 1)
            return false;
        
        OrderId = orderId;
        return true;
    }
    
    public bool SetDiceNumber(ushort dice1, ushort dice2)
    {
        if ((dice1 < 1 || dice1 > 6) || (dice2 < 1 || dice2 > 6))
            return false;
        
        Dice1 = dice1;
        Dice2 = dice2;
        return true;
    }
}