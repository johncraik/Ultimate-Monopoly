using MP.GameEngine.Enums;
using MP.GameEngine.Models;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;

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

    /// <summary>
    /// An ad-hoc (non-turn) dice roll prompted from <paramref name="player"/>, of
    /// <paramref name="diceCount"/> dice (1 or 2). Used by card effects: the dice multiplier on a
    /// money amount (the holder rolls), and the one-die dice-off that picks a highest/lowest roller.
    /// Unlike <see cref="RollTurnDice"/> it does <b>not</b> set <c>TurnDiceRoll</c> — it's an input
    /// to a card effect, not the turn's roll — and (for now) emits no <c>DiceRollReceipt</c>: no stat
    /// consumes card rolls yet (<c>MovementStatsService.cardRolls</c> is a 0 TODO), and emitting a
    /// two-die multiplier roll could false-match the dice-number stat. Card-roll receipts land with
    /// the card stats.
    /// </summary>
    public async Task<DiceRoll> RollCardDice(Framework.GameEngine engine, PlayerModel player, ushort diceCount,
        string title, string body, CancellationToken ct)
    {
        var dice = await engine.PromptProvider.RequestAsync(new DiceRollPrompt
        {
            PlayerId = player.PlayerId,
            Title = title,
            Body = body,
            DiceCount = diceCount
        }, ct);

        // The non-turn DiceRoll ctor (1 or 2 dice, no third die) — RollType is Normal, IsTurnRoll false.
        return diceCount >= 2 ? new DiceRoll(dice.Die1, dice.Die2) : new DiceRoll(dice.Die1);
    }
}