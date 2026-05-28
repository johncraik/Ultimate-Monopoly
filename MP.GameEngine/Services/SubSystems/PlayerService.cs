using System.Data;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

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

    public void ModifyBalance(PlayerModel player, long amount)
    {
        var newBalance = player.Money + amount;
        if (newBalance < 0)
            //TODO call for shortfall prompt
            return;
        else
            player.Money = (uint)newBalance;
    }
    
    
    public async Task ResolveDiceNumber(Framework.GameEngine engine, string playerId, CancellationToken ct)
    {
        var player = engine.Cache.Game.GetPlayer(playerId);
        if (player == null) throw new InvalidOperationException($"Player with id {playerId} not found in game players list.");
        
        var theyRolled = engine.Cache.Game.Metadata.CurrentPlayerId == playerId;
        /*_ = await engine.PromptProvider.Acknowledge(playerId, "YOUR NUMBER!",
            $"{(theyRolled ? "You rolled" : "Someone else rolled")} your number ({player.Dice1} and {player.Dice2})." +
            $"You will collect {RuleDictionary.Currency}{RuleDictionary.DiceNumRolledBonus} from the bank, " +
            $"{(theyRolled ? $"{RuleDictionary.Currency}{RuleDictionary.DiceNumRolledBonus} from each player, " : "")}" +
            $"and a third card at the end of this turn.",
            ct: ct);*/
        
        
    }
}