using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services;

public class PlayerService
{
    public PlayerService()
    {
        
    }
    
    public List<PlayerModel> GetPlayers(List<PlayerDTO> playerDtos)
        => playerDtos.Select(playerDto => new PlayerModel
        {
            PlayerId = playerDto.Id,
            OrderId = playerDto.OrderId,
            Dice1 = playerDto.Dice1,
            Dice2 = playerDto.Dice2,
            Money = RuleDictionary.StartingMoney,
            TripleBonus = RuleDictionary.DefaultTripleBonus,
            JailCost = RuleDictionary.DefaultJailCost,
            
            //Explicit defaults:
            HasPassedInitialGo = false,
            BoardIndex = 0,
            Direction = PlayerDirection.Clockwise,
            DoublesInRow = 0,
            TriplesInRow = 0,
            TurnsToMiss = 0,
            JailTurnCounter = 0,
            MaxJailTurnsOverride = null,
            IsBankrupt = false
        }).ToList();
}