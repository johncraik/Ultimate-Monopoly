using MP.GameEngine.Enums;
using MP.GameEngine.Models;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;

namespace MP.GameEngine.Services.SubSystems;

public class DiceService
{
    
    public async Task<DiceRoll> RollTurnDice(Framework.GameEngine engine, CancellationToken ct)
    {
        var player = engine.Cache.Game.CurrentPlayer();
        if (player == null) throw new InvalidOperationException($"Current player not found in game players list.");
        
        var dice = await engine.PromptProvider.RequestAsync(new DiceRollPrompt
        {
            PlayerId = player.PlayerId,
            Title = "Its Your Turn",
            Body = "Roll the dice to start your turn",
            DiceCount = 3
        }, ct);
        
        if(dice.Die2 == null || dice.ThirdDie == null)
            throw new InvalidOperationException("Dice roll is not complete");
        
        var roll = engine.Cache.SetTurnDiceRoll(dice.Die1, (ushort)dice.Die2, (ushort)dice.ThirdDie);
        if (roll is null) throw new InvalidOperationException("Dice roll is not valid");
        
        engine.EventEmitter.Emit(new DiceRollReceipt(player.PlayerId, roll));
        return roll;
    }
}