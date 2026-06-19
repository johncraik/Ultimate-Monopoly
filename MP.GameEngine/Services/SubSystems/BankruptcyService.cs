using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class BankruptcyService
{
    private readonly IGameCompletionService _completionService;
    private readonly TransactionService _transactionService;
    private readonly PropertyTransferService _propertyTransferService;
    private readonly PropertyService _propertyService;

    public BankruptcyService(IGameCompletionService completionService,
        TransactionService transactionService,
        PropertyTransferService propertyTransferService,
        PropertyService propertyService)
    {
        _completionService = completionService;
        _transactionService = transactionService;
        _propertyTransferService = propertyTransferService;
        _propertyService = propertyService;
    }

    public async Task DeclareBankruptcyFromShortfall(Framework.GameEngine engine, PlayerModel player, uint bankruptAmount, 
        string? owedToPlayerId, CancellationToken ct)
    {
        if(player.IsBankrupt)
            return;
        
        engine.EventEmitter.Emit(new PlayerBankruptedReceipt
        {
            PlayerId = player.PlayerId,
            VoluntaryBankruptcy = false,
            PlayerBalance = player.Money,
            BankruptAmountBy = bankruptAmount
        });

        if (string.IsNullOrEmpty(owedToPlayerId))
        {
            await ProcessBankruptcy(engine, player, ct);
            return;
        }
        
        var counterpartyPlayer = engine.Cache.Game.GetPlayer(owedToPlayerId);
        if (counterpartyPlayer == null)
        {
            await ProcessBankruptcy(engine, player, ct);
            return;
        }
        
        engine.CiteRule(RuleCode.Bankruptcy_CreditorPaidByBank);
        await _transactionService.ReceiveFromBankruptPlayer(engine, counterpartyPlayer, bankruptAmount, ct);

        await ProcessBankruptcy(engine, player, ct);
    }


    public async Task DeclareBankruptcy(Framework.GameEngine engine, string playerId, CancellationToken ct)
    {
        var player = engine.Cache.Game.GetPlayer(playerId);
        if (player == null) return; //Already bankrupt
        
        engine.EventEmitter.Emit(new PlayerBankruptedReceipt
        {
            PlayerId = player.PlayerId,
            VoluntaryBankruptcy = true,
            PlayerBalance = player.Money
        });
        
        await ProcessBankruptcy(engine, player, ct);
    }
    
    private async Task ProcessBankruptcy(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Bankruptcy", 
            "You have declared bankruptcy.", timeout: TimeSpan.FromSeconds(30), ct: ct);
        
        var currentPlayer = engine.Cache.Game.CurrentPlayer();
        player.IsBankrupt = true;
        //Moves all properties back to the bank:
        _propertyTransferService.Bankrupt(engine, player);
        _propertyService.NormaliseProperties(engine);
        
        //Hand back all cards:
        foreach (var card in player.Cards)
        {
            //Reset card groups:
            foreach (var g in card.Groups)
            {
                g.TurnsRemaining = g.TurnsActive;
                g.IsChosenGroup = false;
            }
            engine.Cache.Game.CardDecks.HandBack(card.CardType, card);
        }
        //Empty list:
        player.Cards.Clear();
        
        engine.CiteRule(RuleCode.Bankruptcy_Declared);
        engine.CiteRule(RuleCode.Bankruptcy_AssetsToBank);
        
        //Check if game completed:
        var otherPlayers = engine.Cache.Game.GetPlayers(excludePovPlayer: false);
        if(otherPlayers.Count > 1)
        {
            if(player.PlayerId != currentPlayer?.PlayerId)
                //Player bankrupting was not current player; so do not mutate turn state
                return;
            
            //End players turn (they were current player), and transition to next player
            engine.Cache.SetTurnState(TurnState.EndOfTurn);
            await engine.TurnStateProvider.TransitionToNextPlayer();
            return;
        }
        
        engine.CiteRule(RuleCode.Bankruptcy_LastPlayerWins);
        _ = await engine.PromptProvider.Acknowledge(otherPlayers[0].PlayerId, "Winner!",
            "You are the last player remaining in the game!", timeout: TimeSpan.FromSeconds(30), ct: ct);
        
        //Only one non-bankrupted player remains, game complete:
        await engine.TurnStateProvider.TransitionToFinalTurn();
        await _completionService.DeclareWinner(engine);
    }
}