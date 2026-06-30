using MP.GameEngine.Enums;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Cards;

namespace MP.GameEngine.Services.SubSystems;

public class DiceService
{
    private readonly CardTriggerService _triggerService;

    public DiceService(CardTriggerService triggerService)
    {
        _triggerService = triggerService;
    }
    
    public async Task<(DiceRoll Roll, SuppressDefault SuppressDefault)> RollTurnDice(Framework.GameEngine engine, CancellationToken ct)
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

        var sd = new SuppressDefault();
        switch (roll.RollType)
        {
            case DiceRollType.Double when roll.Die1 == 1:
                var suppressSnakeEyes = await _triggerService.OnSnakeEyes(engine, player, ct);
                var suppressDouble = await _triggerService.OnRollDouble(engine, player, ct);
                sd.Aggregate(suppressSnakeEyes);
                sd.Aggregate(suppressDouble);
                break;
            case DiceRollType.Double:
                var suppressDefaultDouble = await _triggerService.OnRollDouble(engine, player, ct);
                sd.Aggregate(suppressDefaultDouble);
                break;
            case DiceRollType.Triple:
                var suppressTriple = await _triggerService.OnRollTriple(engine, player, ct);
                var suppressOtherTriple = await _triggerService.OnOtherRollsTriple(engine, player, ct);
                sd.Aggregate(suppressTriple);
                sd.Aggregate(suppressOtherTriple);
                break;
        }

        engine.EventEmitter.Emit(new DiceRollReceipt(player.PlayerId, roll));
        return (engine.Cache.GetTurnDiceRoll() ?? throw new InvalidOperationException("Turn dice roll not set"), sd);
    }

    /// <summary>
    /// An ad-hoc (non-turn) dice roll prompted from <paramref name="player"/>, of
    /// <paramref name="diceCount"/> dice (1 or 2). Used by card effects: the dice multiplier on a
    /// money amount (the holder rolls), and the one-die dice-off that picks a highest/lowest roller.
    /// Unlike <see cref="RollTurnDice"/> it does <b>not</b> set <c>TurnDiceRoll</c> — it's an input to a
    /// card effect, not the turn's roll. It <b>does</b> emit a <c>DiceRollReceipt</c> (IsTurnRoll false,
    /// RollType Normal) so card rolls feed <c>MovementStatsService.cardRolls</c> (M-07); the turn-roll /
    /// doubles / triples and dice-number stats are all gated to turn rolls, so card rolls never pollute them.
    /// </summary>
    public async Task<DiceRoll> RollCardDice(Framework.GameEngine engine, PlayerModel player, ushort diceCount,
        string title, string body, CancellationToken ct)
    {
        diceCount = Math.Min((ushort)2, diceCount);
        var dice = await engine.PromptProvider.RequestAsync(new DiceRollPrompt
        {
            PlayerId = player.PlayerId,
            Title = title,
            Body = body,
            DiceCount = diceCount
        }, ct);

        // The non-turn DiceRoll ctor (1 or 2 dice, no third die) — RollType is Normal, IsTurnRoll false.
        var roll = diceCount >= 2 ? new DiceRoll(dice.Die1, dice.Die2) : new DiceRoll(dice.Die1);

        // Emit a DiceRollReceipt so card rolls feed the card-roll stat. Sharing the turn-roll receipt type
        // is safe: a card roll is never a turn roll (no third die → IsTurnRoll false, RollType Normal), so
        // the turn-roll / doubles / triples counters skip it and the dice-number stat is gated to turn rolls.
        engine.EventEmitter.Emit(new DiceRollReceipt(player.PlayerId, roll));
        return roll;
    }

    /// <summary>
    /// A generic dice-off among <paramref name="candidates"/>: each rolls <paramref name="diceCount"/>
    /// dice (1 or 2), and the highest (<paramref name="highest"/> true) or lowest total wins. The
    /// candidate list is caller-supplied and <b>may include the current player</b>. Ties resolve to the
    /// earliest candidate in the list (strict comparison keeps the first). A single candidate wins
    /// without rolling; an empty list returns null. The shared picker behind every card dice-off —
    /// money counterparties, triple-bonus redirect, and the <c>LowestRoller</c>/<c>HighestRoller</c>
    /// target modes.
    /// </summary>
    /// <param name="engine">The game engine bundle the rolls prompt against.</param>
    /// <param name="candidates">The players rolling off (caller-built; may include the holder).</param>
    /// <param name="diceCount">Dice each candidate rolls — 1 or 2.</param>
    /// <param name="highest">True picks the highest total; false the lowest.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<PlayerModel?> RollDiceOff(Framework.GameEngine engine, IReadOnlyList<PlayerModel> candidates,
        ushort diceCount, bool highest, CancellationToken ct)
    {
        // A single candidate wins by default — no point prompting a one-player dice-off.
        if (candidates.Count == 1)
            return candidates[0];

        PlayerModel? winner = null;
        var best = highest ? int.MinValue : int.MaxValue;

        foreach (var candidate in candidates)
        {
            var roll = await RollCardDice(engine, candidate, diceCount, "Dice-off", "Roll for the card dice-off.", ct);
            var value = roll.Die1 + (roll.Die2 ?? 0);

            if (highest ? value > best : value < best)
            {
                best = value;
                winner = candidate;
            }
        }

        return winner;
    }

    /// <summary>
    /// Resolves a card <see cref="DiceOff"/> to its winning player: builds the candidate pool from the
    /// holder (including or excluding the holder per <see cref="DiceOff.IncludeHolder"/>) and rolls the
    /// dice-off via <see cref="RollDiceOff"/>. Returns null when the pool is empty. The single seam every
    /// card dice-off counterparty/target goes through.
    /// </summary>
    public Task<PlayerModel?> ResolveDiceOffTarget(Framework.GameEngine engine, PlayerModel holder, DiceOff diceOff, CancellationToken ct)
    {
        var candidates = engine.Cache.Game.GetPlayers(holder.PlayerId, excludePovPlayer: !diceOff.IncludeHolder);
        return candidates.Count == 0
            ? Task.FromResult<PlayerModel?>(null)
            : RollDiceOff(engine, candidates, diceOff.DiceCount, diceOff.Highest, ct);
    }
}