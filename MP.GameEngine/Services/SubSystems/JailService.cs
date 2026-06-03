using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;

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
    
    public async Task SendPlayerToJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //Going to jail
        //Reset counters, and send player to jail:
        player.DoublesInRow = 0;
        player.TriplesInRow = 0;
        await _movementService.SendPlayerToJail(engine, player, ct);
    }

    public async Task CheckAndLeaveJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        if(!player.IsInJail)
            return;

        //Reset jail counter to 0
        player.JailTurnCounter = 0;
        
        //Direction of travel is no-op (moving from jail -> just visiting);
        //Passing counter-direction to avoid accidental GO bonus
        await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace, 
            PlayerMovementDirection.CounterDirectionOfTravel, ct);
    }
    
    
    public async Task ForcePlayerToLeaveJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //Round the cost for front-end prompt:
        var jailCost = MoneyHelper.NormaliseAmount(player.JailCost, engine.Cache.RoundingRule, FinancialReason.JailFee);
        
        var response = await engine.PromptProvider.RequestAsync(new LeaveJailPrompt
        {
            PlayerId = player.PlayerId,
            Title = "Leave Jail",
            Body = "You have stayed in jail for too long. You must leave jail by choosing from the options below",
            Cost = jailCost,
            HasCard = player.Cards.Count > 0 //TODO - when cards are implemented, this needs to check if get out of jail card exists
        }, ct);

        if (response.Action == LeaveJailAction.PayFee)
        {
            await LeaveJailByPaying(engine, player, ct);
        }
        else
        {
            //TODO - needs to remove the card from player's card deck, and add back into games' card list decks
            await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace, 
                PlayerMovementDirection.CounterDirectionOfTravel, ct);
        }
    }

    public async Task LeaveJailByPaying(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        await _transactionService.PayJailFee(engine, player, ct);
        await _movementService.AdvancePlayer(engine, player, IndexHelper.JustVisitingSpace, 
            PlayerMovementDirection.CounterDirectionOfTravel, ct);

        //Increase jail cost by 50% of original cost
        var increase = Math.Round((player.JailCost * RuleDictionary.JailCostMultiplier), MidpointRounding.AwayFromZero);
        player.JailCost += (uint)increase;

        var increaseDisplay = MoneyHelper.NormaliseAmountToPositive((long)increase, engine.Cache.RoundingRule, FinancialReason.JailFee);
        var costDisplay = MoneyHelper.NormaliseAmount(player.JailCost, engine.Cache.RoundingRule, FinancialReason.JailFee);
        
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Jail Fee Increased", 
            $"Your cost to leave jail has increased by {RuleDictionary.Currency}{increaseDisplay}, " +
            $"and is now {RuleDictionary.Currency}{costDisplay}", ct: ct);
    }
}