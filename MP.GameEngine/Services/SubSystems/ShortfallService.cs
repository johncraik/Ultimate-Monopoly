using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class ShortfallService : IShortfallService
{
    private readonly PropertyCommandService _propCommandService;
    private readonly BuildingService _buildingService;
    private readonly LoanService _loanService;
    private readonly DealService _dealService;
    private readonly BankruptcyService _bankruptcyService;

    public ShortfallService(PropertyCommandService propCommandService,
        BuildingService buildingService,
        LoanService loanService,
        DealService dealService,
        BankruptcyService bankruptcyService)
    {
        _propCommandService = propCommandService;
        _buildingService = buildingService;
        _loanService = loanService;
        _dealService = dealService;
        _bankruptcyService = bankruptcyService;
    }
    
    private async Task<List<ushort>> TargetPropertyPrompt(Framework.GameEngine engine, string playerId, string title, string body, 
        List<PropertyModel> properties, CancellationToken ct)
    {
        var response = await engine.PromptProvider.RequestAsync(new TargetPropertyPrompt
        {
            PlayerId = playerId,
            Title = title,
            Body = body,
            EligibleBoardIndexes = properties.Select(p => p.BoardIndex).ToList(),
            Count = 1
        }, ct: ct);

        return response.SelectedBoardIndexes.ToList();
    }

    /// <summary>
    /// Opens a <see cref="ShortfallPrompt"/> and dispatches the chosen action to the
    /// relevant sub-service.
    /// </summary>
    public async Task<ShortfallOutcome> ResolveShortfall(
        Framework.GameEngine engine,
        PlayerModel player,
        uint amountOwed,
        string? owedToPlayerId,
        ushort? counterpartyPropertyIndex,
        CancellationToken ct)
    {
        var remainingShortfall = player.Money >= amountOwed 
            ? 0 
            : amountOwed - player.Money;
        
        while (remainingShortfall > 0)
        {
            var response = await engine.PromptProvider.RequestAsync(new ShortfallPrompt
            {
                PlayerId = player.PlayerId,
                Title = "You can't afford this",
                Body = $"You owe {RuleDictionary.Currency}{amountOwed} but only have {RuleDictionary.Currency}{player.Money}. Choose how to settle.",
                PlayerBalance = player.Money,
                Cost = amountOwed,
                OwedToPlayerId = owedToPlayerId
            }, ct);

            switch (response.Action)
            {
                case ShortfallAction.TakeLoan:
                    var outcome = await ResolveViaLoan(engine, player, remainingShortfall, ct);
                    if(outcome is not null) 
                        //Loans ALWAYS raise enough funds to pay the shortfall (if can take one out), so we can just return here.
                        return outcome.Value;
                    
                    break;

                case ShortfallAction.Mortgage:
                    await ResolveViaMortgage(engine, player, ct);
                    remainingShortfall = player.Money >= amountOwed ? 0 : amountOwed - player.Money;
                    break;

                case ShortfallAction.SellHouses:
                    await ResolveViaSell(engine, player, ct);
                    remainingShortfall = player.Money >= amountOwed ? 0 : amountOwed - player.Money; 
                    break;

                case ShortfallAction.ProposeDeal:
                    if(string.IsNullOrEmpty(owedToPlayerId))
                        break;
                    
                    var counterpartyPlayer = engine.Cache.Game.GetPlayer(owedToPlayerId);
                    if (counterpartyPlayer == null)
                        //No counterparty player, therefore a no-op
                        break;
                    
                    var accepted = await _dealService.DealForShortfall(engine, player, counterpartyPlayer, ct);
                    if (accepted) return ShortfallOutcome.DebtSettled;

                    //Deal not accepted, therefore a no-op
                    break;
                    
                case ShortfallAction.DeclareBankruptcy:
                    await _bankruptcyService.DeclareBankruptcyFromShortfall(engine, player, amountOwed, owedToPlayerId, ct);
                    return ShortfallOutcome.Bankrupted;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(response.Action), response.Action, "Unhandled shortfall action.");
            }
        }

        return ShortfallOutcome.FundsRaised;
    }


    private async Task<ShortfallOutcome?> ResolveViaLoan(Framework.GameEngine engine, PlayerModel player, uint shortfall, CancellationToken ct)
    {
        var result = await _loanService.TakeLoanForShortfall(engine, player, shortfall, ct);
        return result ? ShortfallOutcome.FundsRaised : null;
    }
    
    
    
    private async Task ResolveViaMortgage(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var props = engine.Cache.Game.GetOwnedProperties(player.PlayerId, 
            includeMortgaged: false, includeReserved: false);
        props = props.Where(p => p is
        {
            State: PropertyState.Owned,
            RentLevel: RentLevel.SET or RentLevel.SINGLE
        }).ToList();

        var sets = PropertySetHelper.GetOwnedSets(player.PlayerId, props);
        props = props.Where(p => !sets.Contains(PropertySetHelper.ResolveSet(p.BoardIndex) 
                                                //Shouldnt be null; but resolve to utility incase (not buildable anyway, can always be mortgaged)
                                                ?? PropertySet.Utility)).ToList();

        if (props.Count == 0)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "No Properties to Mortgage",
                "You do not have an valid properties to mortgage.", ct: ct);
            return;
        }
        
        var selected = await TargetPropertyPrompt(engine, player.PlayerId,
            "Mortgage a Property to Pay the Shortfall", 
            "You must select a property below to mortgage",
            props, ct);

        foreach (var i in selected)
        {
            await _propCommandService.MortgageProperty(engine, i, ct, player.PlayerId);
        }
    }

    
    private async Task ResolveViaSell(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var props = engine.Cache.Game.GetOwnedProperties(player.PlayerId, 
            includeMortgaged: false, includeReserved: false);
        props = props.Where(p => p is
        {
            State: PropertyState.Owned, 
            RentLevel: > RentLevel.SET and <= RentLevel.DOUBLE_HOTEL
        }).ToList();

        var validProps = (from p in props 
            let canSell = engine.Cache.Game.CanDecreaseRentLevel(player.PlayerId, p.BoardIndex) 
            where canSell 
            select p).ToList();
        
        if (validProps.Count == 0)
        {
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "No Houses/Hotels to Sell",
                "You do not have an valid properties to sell houses/hotels.", ct: ct);
            return;
        }
        
        var selected = await TargetPropertyPrompt(engine, player.PlayerId,
            "Sell on a Property to Pay the Shortfall", 
            "You must select a property below to sell a house/hotel",
            validProps, ct);

        foreach (var i in selected)
        {
            await _buildingService.SellOnProperty(engine, i, ct, player.PlayerId);
        }
    }
}