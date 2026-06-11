using MP.GameEngine.Models.Deals;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class DealService
{
    private readonly TransactionService _transactionService;
    private readonly PropertyTransferService _propertyTransferService;
    private readonly PropertyService _propertyService;

    public DealService(TransactionService transactionService,
        PropertyTransferService propertyTransferService, 
        PropertyService propertyService)
    {
        _transactionService = transactionService;
        _propertyTransferService = propertyTransferService;
        _propertyService = propertyService;
    }

    public async Task<bool> DealForShortfall(Framework.GameEngine engine, PlayerModel player, 
        PlayerModel counterpartyPlayer, CancellationToken ct)
    {
        var playerProps = engine.Cache.Game.TradableProperties(player.PlayerId, includeMortgaged: true);
        var counterpartyProps = engine.Cache.Game.TradableProperties(counterpartyPlayer.PlayerId, includeMortgaged: true);

        var response = await engine.PromptProvider.RequestAsync(new BuildDealPrompt
        {
            PlayerId = player.PlayerId,
            Title = "Deal Offer",
            Body = "Offer a deal to pay for your shortfall",
            ProposerBalance = player.Money,
            ProposerDealableIndexes = playerProps.Select(p => p.BoardIndex).ToList(),
            CounterPartyId = counterpartyPlayer.PlayerId,
            CounterPartyBalance = counterpartyPlayer.Money,
            CounterPartyDealableIndexes = counterpartyProps.Select(p => p.BoardIndex).ToList()
        }, ct: ct);
        
        if(response.Cancelled)
            return false;

        return await RunDeal(engine, player, counterpartyPlayer, response.Contents, ct);
    }

    /// <summary>
    /// Turn-boundary deal command (game-deals.md §6). The proposer built the deal client-side
    /// and submitted it whole, so — unlike the shortfall path — there is no prompt validator
    /// behind it: each side's offer is validated against its dealable pool and cash (§10)
    /// before the deal runs (open the accept/decline <see cref="DealPrompt"/> to the counter
    /// party, apply on accept). No-ops if either player is bankrupt or an offer is invalid.
    /// </summary>
    public async Task ProposeDealCommand(Framework.GameEngine engine, string proposerId, string counterPartyId,
        DealContents contents, CancellationToken ct)
    {
        var game = engine.Cache.Game;
        var proposer = game.GetPlayer(proposerId);
        var counterparty = game.GetPlayer(counterPartyId);
        if (proposer is null || counterparty is null) return;

        if (!ValidOffer(game, proposer, contents.MoneyFromProposer, contents.PropertiesFromProposer)) return;
        if (!ValidOffer(game, counterparty, contents.MoneyFromCounterParty, contents.PropertiesFromCounterParty)) return;

        await RunDeal(engine, proposer, counterparty, contents, ct);
    }

    /// <summary>
    /// A side's offer is valid when its money comes from cash on hand and every offered
    /// property is in that player's dealable pool (owned, not reserved, not built-on; mortgaged
    /// allowed) — reusing <see cref="GameModel.TradableProperties(string, Enums.Properties.PropertySet?, bool)"/>,
    /// the same pool the builder grid is drawn from.
    /// </summary>
    private static bool ValidOffer(GameModel game, PlayerModel player, uint money, IReadOnlyList<ushort> properties)
    {
        if (money > player.Money) return false;

        var dealable = game.TradableProperties(player.PlayerId, includeMortgaged: true)
            .Select(p => p.BoardIndex).ToHashSet();
        return properties.All(dealable.Contains);
    }

    private async Task<bool> RunDeal(Framework.GameEngine engine, PlayerModel player, PlayerModel counterpartyPlayer, 
        DealContents contents, CancellationToken ct)
    {
        var response = await engine.PromptProvider.RequestAsync(new DealPrompt
        {
            PlayerId = counterpartyPlayer.PlayerId,
            ProposerId = player.PlayerId,
            Title = "Deal Offer",
            Body = "Would you like to accept this deal?",
            Contents = contents
        }, ct: ct);
        
        if(!response.Accept)
        {
            //Let player know that deal has been declined
            _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Deal Declined", "The deal has been declined.", 
                timeout: TimeSpan.FromSeconds(30), ct: ct);
            return false;
        }

        //Let player know that deal has been accepted
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Deal Accepted", "The deal has been accepted.", 
            timeout: TimeSpan.FromSeconds(30), ct: ct);
        
        //Send money from player to counterparty player:
        if(contents.MoneyFromProposer > 0)
            await _transactionService.ProcessDealPayment(engine, player, counterpartyPlayer, contents.MoneyFromProposer, ct);
        
        //Send money from counterparty player to player:
        if(contents.MoneyFromCounterParty > 0)
            await _transactionService.ProcessDealPayment(engine, counterpartyPlayer, player, contents.MoneyFromCounterParty, ct);
        
        //Give counterparty player properties from player:
        if(contents.PropertiesFromProposer.Count > 0)
            foreach (var i in contents.PropertiesFromProposer)
            {
                var property = engine.Cache.Game.GetPropertySpace(i);
                if(property == null)
                    continue;
                
                _propertyTransferService.Transfer(engine, player, counterpartyPlayer, property);
            }
        
        if(contents.PropertiesFromCounterParty.Count == 0)
            return CompleteDeal(engine, player.PlayerId, counterpartyPlayer.PlayerId);

        //Give player properties from counterparty player:
        foreach (var i in contents.PropertiesFromCounterParty)
        {
            var property = engine.Cache.Game.GetPropertySpace(i);
            if(property == null)
                continue;
            
            _propertyTransferService.Transfer(engine, counterpartyPlayer, player, property);
        }
        
        return CompleteDeal(engine, player.PlayerId, counterpartyPlayer.PlayerId);
    }

    private bool CompleteDeal(Framework.GameEngine engine, string playerId, string counterpartyPlayerId)
    {
        engine.Cache.Game.CheckReservationRuleSetObtained(playerId);
        engine.Cache.Game.CheckReservationRuleSetObtained(counterpartyPlayerId);
        
        _propertyService.NormaliseProperties(engine);
        return true;
    }
}