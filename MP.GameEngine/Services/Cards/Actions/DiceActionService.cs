using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="DiceAction"/> (cards-design.md §3 Dice). Today it handles the
/// triple-bonus <i>payout</i> family (<see cref="DiceKind.ModifyTripleBonus"/>): it works out the
/// payout factor / recipient — rolling a die for "×die", or a one-die dice-off for the
/// "lowest roller receives it" redirect — and applies it through the split
/// <see cref="PlayerService.ApplyTripleBonus"/>. The accumulator still increments there.
///
/// The roll-type conversions (<see cref="DiceKind.ConvertDoubleToTriple"/> /
/// <see cref="DiceKind.DowngradeTripleToDouble"/>) are applied in <c>PlayerTurnOrchestrator</c>
/// before the doubles/triples-in-a-row counters update — wired in the orchestrator pass.
/// </summary>
public class DiceActionService : ICardActionService<DiceAction>
{
    private readonly PlayerService _playerService;
    private readonly DiceService _diceService;
    private readonly CardImmunityService _immunityService;

    /// <summary>Creates the dice-action handler over the triple-bonus resolution and dice seams.</summary>
    public DiceActionService(PlayerService playerService, 
        DiceService diceService,
        CardImmunityService immunityService)
    {
        _playerService = playerService;
        _diceService = diceService;
        _immunityService = immunityService;
    }

    /// <summary>Dispatches by kind.</summary>
    public async Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, DiceAction action, CancellationToken ct, CardActionContext? context = null)
    {
        switch (action.Kind)
        {
            case DiceKind.ModifyTripleBonus:
                await ModifyTripleBonus(engine, player, action, ct);
                break;

            case DiceKind.ConvertDoubleToTriple:
                engine.CiteRule(RuleCode.Event_ConvertedToTriple);
                engine.Cache.Game.ModifiedDiceRollType = DiceRollType.Triple;
                break;
            case DiceKind.DowngradeTripleToDouble:
                engine.CiteRule(RuleCode.Event_DowngradedToDouble);
                engine.Cache.Game.ModifiedDiceRollType = DiceRollType.Double;
                break;
        }
        
        return true;
    }

    /// <summary>
    /// Resolves the triple-bonus payout modifier for the targeted player and applies it via
    /// <see cref="PlayerService.ApplyTripleBonus"/>: a fixed factor (0 suppress / 2 double), a one-die
    /// multiplier, or a redirect of the (full) payout to the dice-off lowest roller.
    /// </summary>
    private async Task ModifyTripleBonus(Framework.GameEngine engine, PlayerModel holder, DiceAction action, CancellationToken ct)
    {
        // The triple-bonus owner — the holder, or a chosen player ("cancel a player's triple bonus").
        var target = (await CardActionHelper.ResolveTargets(engine, holder, action.Target, ct)).FirstOrDefault();
        if (target is null)
            return;

        ushort factor;
        PlayerModel? recipient = null;

        if (action.PayoutRedirectToLowestRoller)
        {
            // Every player rolls one die (incl. the owner); the lowest roller receives the (full) bonus.
            // The owner still accrues the accumulator. A self-win (owner rolls lowest) leaves it with them.
            recipient = await _diceService.ResolveDiceOffTarget(engine, target,
                new DiceOff { Highest = false, IncludeHolder = true }, ct);
            factor = 1;
        }
        else if (action.PayoutMultiplyByDie)
        {
            var roll = await _diceService.RollCardDice(engine, target, 1,
                "Triple Bonus Multiplier", "Roll one die to multiply your triple bonus.", ct);
            factor = roll.Die1;
        }
        else
        {
            // Fixed factor — 0 suppresses/cancels, 2 doubles. Null defaults to the normal payout.
            factor = action.PayoutFactor ?? 1;
        }

        if (factor == 0)
        {
            //Check immunity card and if played
            var result = await _immunityService.CheckCancelledTripleBonusImmunity(engine, target, ct);
            if (result)
            {
                engine.Notifier.Notify(engine.Cache.GameId, holder.PlayerId, 
                    "Player played an immunity card. Triple bonus is not cancelled");
                engine.Notifier.Notify(engine.Cache.GameId, target.PlayerId, 
                    "You played an immunity card. Triple bonus is not cancelled");
                return;
            }
        }
        
        await _playerService.ApplyTripleBonus(engine, target, factor, recipient, ct);
    }
}