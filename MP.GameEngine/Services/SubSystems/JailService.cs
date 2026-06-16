using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Services.SubSystems;

public class JailService
{
    private readonly MovementService _movementService;
    private readonly TransactionService _transactionService;

    public JailService(MovementService movementService,
        TransactionService transactionService)
    {
        _movementService = movementService;
        _transactionService = transactionService;
    }
    
    public async Task<bool> SendPlayerToJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //Going to jail
        //Reset counters, and send player to jail:
        player.DoublesInRow = 0;
        player.TriplesInRow = 0;

        if (engine.Cache.Game.GlobalEventInfo.JailFull)
        {
            //Jail is full event
            engine.CiteRule(RuleCode.Event_Jail);
            
            //Round the cost for front-end prompt:
            var jailCost = MoneyHelper.NormaliseAmount(player.JailCost, engine.Cache.RoundingRule, FinancialReason.JailFee);
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Jail is Full",
                $"The jail is full, so you must pay your jail fee of {RuleDictionary.Currency}{jailCost:N0}", ct: ct);

            await PayJailFee(engine, player, ct);
            //Paid the fee INSTEAD of going to jail — the player isn't moved, and no jail receipt/notification.
            return false;
        }

        await _movementService.SendPlayerToJail(engine, player, ct);
        
        engine.Notifier.Notify(engine.Cache.GameId, player.PlayerId, "You have been sent to jail");
        
        engine.EventEmitter.Emit(new PlayerEnteredJailReceipt
        {
            PlayerId = player.PlayerId
        });
        return true;
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

        //Reset jail counter to 0
        player.JailTurnCounter = 0;
        player.MaxJailTurnsOverride = null;
        player.MinJailTurns = null;
        player.CollectRentInJail = false;
        engine.CiteRule(RuleCode.Jail_LeaveByDouble);
        
        //Direction of travel is no-op (moving from jail -> just visiting);
        //Passing counter-direction to avoid accidental GO bonus
        await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace, 
            PlayerMovementDirection.CounterDirectionOfTravel, ct);
    }
    
    
    public async Task ForcePlayerToLeaveJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //Reset jail counter to 0
        player.JailTurnCounter = 0;
        player.MaxJailTurnsOverride = null;
        player.MinJailTurns = null;
        player.CollectRentInJail = false;
        engine.CiteRule(RuleCode.Jail_ThreeTurnLimit);
        
        //Round the cost for front-end prompt:
        var jailCost = MoneyHelper.NormaliseAmount(player.JailCost, engine.Cache.RoundingRule, FinancialReason.JailFee);

        var getOutOfJailCard = player.GetOutOfJailCard();
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
        
        if(!player.IsInJail)
            return;
        
        await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace, 
            PlayerMovementDirection.CounterDirectionOfTravel, ct);

        await PayJailFee(engine, player, ct);
    }

    private async Task PayJailFee(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
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
        
        if(!player.IsInJail)
            return false;
        
        await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace,
            PlayerMovementDirection.CounterDirectionOfTravel, ct);
        return true;
    }


    public async Task GoToJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var suppressDefault =  await engine.CardService.DrawCard(engine, player, CardType.GoToJail, ct);
        if(suppressDefault) return;
        
        engine.CiteRule(RuleCode.GoToJail_SendToJail);
        await SendPlayerToJail(engine, player, ct);
    }
}