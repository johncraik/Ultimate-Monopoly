using System.Data;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class PlayerService
{
    private readonly TransactionService _transactionService;

    public PlayerService(TransactionService transactionService)
    {
        _transactionService = transactionService;
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
            InitialRoll = true,
            
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

    
    
    public async Task ResolveDiceNumber(Framework.GameEngine engine, string playerId, CancellationToken ct)
    {
        var player = engine.Cache.Game.GetPlayer(playerId);
        if (player == null) throw new InvalidOperationException($"Player with id {playerId} not found in game players list.");
        
        var theyRolled = engine.Cache.Game.Metadata.CurrentPlayerId == playerId;
        _ = await engine.PromptProvider.Acknowledge(playerId, "YOUR NUMBER!",
            $"{(theyRolled ? "You rolled" : "Someone else rolled")} your number ({player.Dice1} and {player.Dice2})." +
            $"You will collect {RuleDictionary.Currency}{RuleDictionary.DiceNumRolledBonus} from the bank, " +
            $"{(theyRolled ? $"{RuleDictionary.Currency}{RuleDictionary.DiceNumRolledBonus} from each player, " : "")}" +
            $"and a third card at the end of this turn.",
            ct: ct);
        
        //Bank transaction:
        await _transactionService.ReceiveDiceBonus(engine, player, ct);
        if(!theyRolled) return;
        
        var otherPlayers = engine.Cache.Game.GetPlayers(playerId);
        foreach (var p in otherPlayers)
        {
            await _transactionService.PayDiceBonus(engine, p, player, ct);
        }
    }
}