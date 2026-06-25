using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Cards;

namespace MP.GameEngine.Services.SubSystems;

public class JailService
{
    private readonly MovementService _movementService;
    private readonly TransactionService _transactionService;
    private readonly CardTriggerService _triggerService;
    private readonly CardImmunityService _immunityService;
    private readonly ICardCacheService _cacheService;

    public JailService(MovementService movementService,
        TransactionService transactionService,
        CardTriggerService triggerService,
        CardImmunityService immunityService,
        ICardCacheService cacheService)
    {
        _movementService = movementService;
        _transactionService = transactionService;
        _triggerService = triggerService;
        _immunityService = immunityService;
        _cacheService = cacheService;
    }
    
    public async Task<bool> SendPlayerToJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //Going to jail
        //Reset counters, and send player to jail:
        player.DoublesInRow = 0;
        player.TriplesInRow = 0;

        var sent = false;
        if (engine.Cache.Game.GlobalEventInfo.JailFull)
        {
            //Jail is full event
            engine.CiteRule(RuleCode.Event_Jail);
            
            //Round the cost for front-end prompt:
            var jailCost = MoneyHelper.NormaliseAmount(player.JailCost, engine.Cache.RoundingRule, FinancialReason.JailFee);
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Jail is Full",
                $"The jail is full, so you must pay your jail fee of {RuleDictionary.Currency}{jailCost:N0}", ct: ct);

            await PayJailFee(engine, player, ct);
        }
        else
        {
            await _movementService.SendPlayerToJail(engine, player, ct);
            
            engine.Notifier.Notify(engine.Cache.GameId, player.PlayerId, "You have been sent to jail");
            sent = true;
        }
        
        engine.EventEmitter.Emit(new PlayerEnteredJailReceipt
        {
            PlayerId = player.PlayerId
        });
        return sent;
    }


    public void ResetPlayerJailFlags(PlayerModel player)
    {
        //Reset jail counter to 0
        player.JailTurnCounter = 0;
        player.MaxJailTurnsOverride = null;
        player.MinJailTurns = null;
        player.CollectRentInJail = false;
    }
    

    public async Task CheckAndLeaveJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        if(!player.IsInJail)
            return;
        
        if(!player.CanLeaveJail)
        {
            engine.CiteRule(RuleCode.Jail_CantLeaveDueToCard);
            return;
        }
        
        _ = await _triggerService.OnInJail(engine, player, ct);

        ResetPlayerJailFlags(player);
        engine.CiteRule(RuleCode.Jail_LeaveByDouble);
        
        //Direction of travel is no-op (moving from jail -> just visiting);
        //Passing counter-direction to avoid accidental GO bonus
        await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace, 
            PlayerMovementDirection.CounterDirectionOfTravel, ct, false);
    }
    
    
    public async Task ForcePlayerToLeaveJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //Reset jail counter to 0
        ResetPlayerJailFlags(player);
        engine.CiteRule(RuleCode.Jail_ThreeTurnLimit);
        
        _ = await _triggerService.OnInJail(engine, player, ct);
        
        //Round the cost for front-end prompt:
        var jailCost = MoneyHelper.NormaliseAmount(player.JailCost, engine.Cache.RoundingRule, FinancialReason.JailFee);

        var getOutOfJailCard = await player.GetOutOfJailCard(_cacheService);
        var response = await engine.PromptProvider.RequestAsync(new LeaveJailPrompt
        {
            PlayerId = player.PlayerId,
            Title = "Leave Jail",
            Body = "You have stayed in jail for too long. You must leave jail by choosing from the options below",
            Cost = jailCost,
            HasCard = getOutOfJailCard != null
        }, ct);

        if (response.Action == LeaveJailAction.PayFee)
            await LeaveJailByPaying(engine, player, ct);
        else if(getOutOfJailCard != null)
            //Play the held Get Out of Jail Free card — PlayCard resolves it (releasing the
            //player) then removes it from hand and returns it to its deck (§9.4).
            await engine.CardService.PlayCard(engine, player, getOutOfJailCard, ct);
        else
            //Unreachable: the LeaveJailPrompt only offers PlayCard when HasCard is true and the
            //validator rejects it otherwise — so a null card here means a forged client response.
            throw new InvalidOperationException("Leave-jail-by-card chosen but the player holds no Get Out of Jail Free card.");
    }

    public async Task LeaveJailByPaying(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        if (!player.CanLeaveJail)
        {
            engine.CiteRule(RuleCode.Jail_CantLeaveDueToCard);
            return;
        }

        ResetPlayerJailFlags(player);
        
        if(!player.IsInJail)
            return;

        //Held jail cards react to the voluntary exit too (e.g. "befriend a guard" waives the fee) before
        //the fee is charged — the other exit paths fire OnInJail already; this one is the command path.
        _ = await _triggerService.OnInJail(engine, player, ct);

        await PayJailFee(engine, player, ct);

        await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace,
            PlayerMovementDirection.CounterDirectionOfTravel, ct, false);
    }

    private async Task PayJailFee(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        if (player.FreeNextJailExit)
        {
            //One-shot waiver ("befriend a guard") — skip the charge and the 50% escalation, consume the flag.
            player.FreeNextJailExit = false;
            engine.CiteRule(RuleCode.Jail_FeeWaivedByCard);
            engine.Notifier.Notify(engine.Cache.GameId, player.PlayerId, "You befriended a guard — no jail fee to leave.");
            return;
        }

        await _transactionService.PayJailFee(engine, player, ct);
        
        //Increase jail cost by 50% of original cost
        var increase = Math.Round((player.JailCost * RuleDictionary.JailCostMultiplier), MidpointRounding.AwayFromZero);
        player.JailCost += (uint)increase;

        var increaseDisplay = MoneyHelper.NormaliseAmountToPositive((long)increase, engine.Cache.RoundingRule, FinancialReason.JailFee);
        var costDisplay = MoneyHelper.NormaliseAmount(player.JailCost, engine.Cache.RoundingRule, FinancialReason.JailFee);
        
        engine.CiteRule(RuleCode.Jail_FeeEscalates);
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Jail Fee Increased", 
            $"Your cost to leave jail has increased by {RuleDictionary.Currency}{increaseDisplay:N0}, " +
            $"and is now {RuleDictionary.Currency}{costDisplay:N0}", ct: ct);
    }
    

    public async Task<bool> LeaveJailByCard(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        if (!player.CanLeaveJail)
        {
            engine.CiteRule(RuleCode.Jail_CantLeaveDueToCard);
            return false;
        }
        
        ResetPlayerJailFlags(player);
        
        if(!player.IsInJail)
            return false;
        
        await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace,
            PlayerMovementDirection.CounterDirectionOfTravel, ct, false);
        return true;
    }


    public async Task GoToJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //Check if this player should be prevented from drawing a Go To Jail card:
        var preventCard = engine.Cache.PreventBoardIndexCard(player.PlayerId, IndexHelper.GoToJailSpace);
        if (!preventCard)
        {
            var result = await _immunityService.CheckGoToJailCardImmunity(engine, player, ct);
            if (result)
            {
                engine.Notifier.Notify(player.PlayerId, player.PlayerId, 
                    "You played an immunity card. You will not get a Go To Jail Card");
            }
            else
            {
                var suppressDefault =  await engine.CardService.DrawCard(engine, player, CardType.GoToJail, ct);
                if(suppressDefault.SuppressGoToJail) return;
            }
        }
        
        engine.CiteRule(RuleCode.GoToJail_SendToJail);
        await SendPlayerToJail(engine, player, ct);
    }
}