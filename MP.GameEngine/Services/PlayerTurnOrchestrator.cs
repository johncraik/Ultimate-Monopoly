using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Cards;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services;

public class PlayerTurnOrchestrator
{
    private readonly DiceService _diceService;
    private readonly TransactionService _transactionService;
    private readonly MovementService _movementService;
    private readonly PlayerService _playerService;
    private readonly JailService _jailService;
    private readonly BoardService _boardService;
    private readonly GlobalEventService _globalEventService;
    private readonly CardTriggerService _triggerService;
    private readonly IGameCompletionService _completionService;
    private readonly ITurnTaxService _turnTaxService;

    public PlayerTurnOrchestrator(DiceService diceService,
        TransactionService transactionService,
        MovementService movementService,
        PlayerService playerService,
        JailService jailService,
        BoardService boardService,
        GlobalEventService globalEventService,
        CardTriggerService triggerService,
        IGameCompletionService completionService,
        ITurnTaxService turnTaxService)
    {
        _diceService = diceService;
        _transactionService = transactionService;
        _movementService = movementService;
        _playerService = playerService;
        _jailService = jailService;
        _boardService = boardService;
        _globalEventService = globalEventService;
        _triggerService = triggerService;
        _completionService = completionService;
        _turnTaxService = turnTaxService;
    }


    public async Task StartPlayerTurn(Framework.GameEngine engine, CancellationToken ct)
    {
        if (engine.Cache.Game.GetPlayers(excludePovPlayer: false).Count == 1)
        {
            //Declares a winner
            await _completionService.DeclareWinner(engine);
            return;
        }
        
        // Defensive no-op: a turn is only kicked off from StartOfTurn. A stale or
        // duplicate StartTurn (e.g. a second tap that queued behind the first)
        // refuses here rather than rolling into the engine and throwing — an
        // illegal mutation no-ops, it does not fault the turn. Mirrors the guard
        // in ResolveThirdDieMovement.
        if (engine.Cache.TurnState != TurnState.StartOfTurn)
            return;
        
        var player = engine.Cache.Game.CurrentPlayer();
        if (player == null) throw new InvalidOperationException("Current player not found in game players list.");
        
        //Apply Turn tax:
        await _turnTaxService.ApplyTax(engine, player, ct);
        
        //Roll dice
        //var 'dice' IS the modified dice roll, if any, otherwise the original
        //var 'suppressDefault' is the result of suppressed defaults from triggers
        var (dice, suppressDefault) = await _diceService.RollTurnDice(engine, ct);
        
        //Start roll movement phase for player
        engine.TurnStateProvider.TransitionToRollMovementPhase();
        
        //RE-GET player since turn state transition saved changes and broke reference
        player = engine.Cache.Game.CurrentPlayer();
        if (player == null) throw new InvalidOperationException("Current player not found in game players list.");
        
        //Existing player is not included, and list is ordered by clockwise from player POV by default
        var otherPlayers = engine.Cache.Game.GetPlayers();

        var playerIdWithMatchingDiceNum = engine.Cache.Game.CheckAnyDiceNumbers(dice);
        if(!string.IsNullOrEmpty(playerIdWithMatchingDiceNum))
            await _playerService.ResolveDiceNumber(engine, playerIdWithMatchingDiceNum, ct);

        if (player.IsInJail)
            player.JailTurnCounter++;

        if (player.BoardIndex != IndexHelper.GoSpace)
            player.InitialRoll = false;
        
        var transitionToThirdDie = true;
        switch (dice.RollType)
        {
            case DiceRollType.Normal:
                await HandleNormalRoll(engine, player, dice, ct);
                break;
            
            case DiceRollType.Double:
                //Clear global event (always happens on a double)
                _globalEventService.ClearCurrentEvent(engine);
                
                if (player.DoublesInRow < RuleDictionary.DoublesBeforeJail)
                {
                    var suppressCard = await engine.CardService.DrawCard(engine, player, CardType.Double, ct);
                    suppressDefault.Aggregate(suppressCard);
                    
                    //Re-get dice roll (double card may have changed roll type):
                    dice = engine.Cache.GetTurnDiceRoll() 
                           ?? throw new InvalidOperationException("Dice roll cannot be null");
                    
                    //Switch roll type again IF card has changed roll type:
                    switch (dice.RollType)
                    {
                        case DiceRollType.Normal:
                            throw new InvalidOperationException("Double rolls cannot be downgraded to a normal roll");
                        case DiceRollType.Triple:
                            //Credit the triple bonus since double was upgraded to triple
                            await _playerService.ResolveTripleBonus(engine, player, ct);
                            
                            await HandleTripleRoll(engine, player, dice, ct);
                            transitionToThirdDie = false;
                            break;
                        default:
                            await HandleDoubleRoll(engine, player, otherPlayers, dice, suppressDefault, ct);
                            break;
                    }
                    
                    if (!suppressDefault.SuppressDirectionChange && engine.Cache.Game.ModifiedDiceRollType == null)
                    {
                        //Suppress default is "do not turn around" or "double upgraded to triple"
                        player.FlipDirection(engine);
                    }
                }
                else
                {
                    //Too many doubles in a row, so send player to jail:
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Going to Jail!", "You have rolled too many doubles in a row.", ct: ct);
                    
                    //Cite rule and send to jail:
                    engine.CiteRule(RuleCode.Double_ThreeInRowToJail);
                    await _jailService.SendPlayerToJail(engine, player, ct);
                }
                
                break;
            
            case DiceRollType.Triple:
                if (player.TriplesInRow < RuleDictionary.TriplesBeforeJail)
                {
                    transitionToThirdDie = await TripleRoll(engine, player, otherPlayers, dice, suppressDefault, ct);
                }
                else
                {
                    //Too many triples in a row, so send player to jail:
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Going to Jail!", "You have rolled too many triples in a row.", ct: ct);
                    
                    //Cite rule and send to jail:
                    engine.CiteRule(RuleCode.Triple_ThreeInRowToJail);
                    var sentToJail = await _jailService.SendPlayerToJail(engine, player, ct);
                    
                    transitionToThirdDie = !sentToJail && await TripleRoll(engine, player, otherPlayers, dice, suppressDefault, ct);
                }
                
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if(player.InitialRoll && player.BoardIndex != IndexHelper.GoSpace)
            player.InitialRoll = false;
        
        //Check if rolling player gets a third card (they rolled their number):
        if(player.GetThirdCard)
        {
            await engine.CardService.DrawCard(engine, player, CardType.Third, ct);
            engine.CiteRule(RuleCode.Third_Card_Number);
            
            //Reset to false (for next turn/roll):
            player.GetThirdCard = false;
        }
        
        if(transitionToThirdDie)
        {
            engine.TurnStateProvider.TransitionToThirdDie();
            engine.CiteRule(RuleCode.Roll_ThirdDieMovesOthers);
        }
        else
        {
            //Cite triple rules:
            engine.CiteRule(RuleCode.Triple_MovesCombinedNoOthers);
            engine.CiteRule(RuleCode.Triple_NoDirectionChange);
            
            //Triple doesnt grant third die movement
            engine.TurnStateProvider.TransitionToEndOfTurn();
        }
    }

    private async Task HandleNormalRoll(Framework.GameEngine engine, PlayerModel player, DiceRoll dice, CancellationToken ct)
    {
        player.DoublesInRow = 0;
        player.TriplesInRow = 0;

        switch (player.CanLeaveJail)
        {
            case true when player.JailTurnCounter == (player.MaxJailTurnsOverride ?? RuleDictionary.MaxJailTurns):
                //Jail counter already increased before role type switch
                await _jailService.ForcePlayerToLeaveJail(engine, player, ct);
                break;
            case false:
                engine.CiteRule(RuleCode.Jail_CantLeaveDueToCard);
                return;
        }
                
        if (!player.IsInJail)
        {
            //Standard roll, move them normally:
            var movement = (ushort)(dice.Die1 + (dice.Die2 ?? throw new InvalidOperationException("Second die cannot be null")));
            await _movementService.MovePlayer(engine, player, movement, ct);
            await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
            _ = await _triggerService.OnNextMove(engine, player, ct);
        }
    }

    private async Task HandleDoubleRoll(Framework.GameEngine engine, PlayerModel player, List<PlayerModel> otherPlayers, 
        DiceRoll dice, SuppressDefault suppressDefault, CancellationToken ct)
    {
        //Will move player OUT of jail if in jail
        await _jailService.CheckAndLeaveJail(engine, player, ct);
        
        //Get the double effect record, cite rule, and notify player
        var effect = DoubleEffects.For(dice.Die1);
        engine.CiteRule(effect.RuleCode);
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, effect.Title, effect.Body, ct: ct);
        
        //Apply snake eyes bonus (if applicable)
        if(effect.SnakeEyesBonus && !suppressDefault.SuppressSnakeEyes)
            await _transactionService.ReceiveSnakeEyes(engine, player, ct);
        
        //Move the player based on the effect steps:
        if(effect.RollerSteps.Count > 0)
            foreach (var step in effect.RollerSteps)
            {
                await _movementService.MovePlayer(engine, player, step, ct);
                await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
                _ = await _triggerService.OnNextMove(engine, player, ct);
            }
        
        //Increment miss turns if effect requires it
        if(effect.RollerMissesTurn)
            player.TurnsToMiss++;
        
        foreach (var p in otherPlayers)
        {
            //Move other players based on the effect steps:
            if(effect.OtherPlayerSteps.Count > 0)
                foreach (var step in effect.OtherPlayerSteps)
                {
                    //Will only be one step (per player), double foreach not a concern
                    await _movementService.MovePlayer(engine, p, step, ct);
                    await _boardService.ResolveBoardSpaceForPlayer(engine, p, ct);
                    _ = await _triggerService.OnNextMove(engine, p, ct);
                }
            
            //Increment other player's miss turns if effect requires it'
            if(effect.OtherPlayerMissesTurn)
                p.TurnsToMiss++;
        }
    }


    private async Task<bool> TripleRoll(Framework.GameEngine engine, PlayerModel player, List<PlayerModel> otherPlayers,
        DiceRoll dice, SuppressDefault sd, CancellationToken ct)
    {
        var suppressDefault = await engine.CardService.DrawCard(engine, player, CardType.Triple, ct);
        sd.Aggregate(suppressDefault);
        if (!sd.SuppressTripleBonus)
        {
            //Credit the triple bonus, and increase it (default triple bonus - card will credit custom if needed):
            await _playerService.ResolveTripleBonus(engine, player, ct);
        }
        
        //Re-get dice roll (triple card may have changed roll type):
        dice = engine.Cache.GetTurnDiceRoll() 
               ?? throw new InvalidOperationException("Dice roll cannot be null");
                    
        //Switch roll type again IF card has changed roll type:
        switch (dice.RollType)
        {
            case DiceRollType.Normal:
                throw new InvalidOperationException("Triple rolls cannot be downgraded to a normal roll");
            case DiceRollType.Double:
                //The triple was downgraded to a double, so handle it as a double:
                await HandleDoubleRoll(engine, player, otherPlayers, dice, sd, ct);
                return true;
            default:
                await HandleTripleRoll(engine, player, dice, ct);
                return false;
        }
    }
    
    private async Task HandleTripleRoll(Framework.GameEngine engine, PlayerModel player, DiceRoll dice, CancellationToken ct)
    {
        //Will move player OUT of jail if in jail
        await _jailService.CheckAndLeaveJail(engine, player, ct);
                        
        _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Triple!", 
            "You rolled a triple, you will move the combined total of all three dice.", ct: ct);
                        
        var movement = (ushort)((dice.Die1 + dice.Die2 + dice.ThirdDie) ?? throw new InvalidOperationException("Dice roll result cannot be null"));
        await _movementService.MovePlayer(engine, player, movement, ct);
        await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
        _ = await _triggerService.OnNextMove(engine, player, ct);
    }

    


    public async Task ResolveThirdDieMovement(Framework.GameEngine engine, CancellationToken ct)
    {
        if(engine.Cache.TurnState != TurnState.ThirdDieMovement)
            return;
        
        var otherPlayers = engine.Cache.Game.GetPlayers();
        var dice = engine.Cache.GetTurnDiceRoll();
        var thirdDie = dice?.ThirdDie ?? throw new InvalidOperationException("Third die roll cannot be null");

        foreach (var player in otherPlayers)
        {
            if (!player.IsInJail)
            {
                //Move each player based on third die, if not in jail
                await _movementService.MovePlayer(engine, player, thirdDie, ct);
                await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
                _ = await _triggerService.OnNextMove(engine, player, ct);
            }
            
            //Check if player gets a third card (someone else rolled their number):
            if (!player.GetThirdCard) continue;
            
            await engine.CardService.DrawCard(engine, player, CardType.Third, ct);
            engine.CiteRule(RuleCode.Third_Card_Number);

            //Reset to false (for next turn/roll):
            player.GetThirdCard = false;
        }
        
        engine.TurnStateProvider.TransitionToEndOfTurn();
    }

    public async Task EndPlayerTurn(Framework.GameEngine engine, CancellationToken ct)
    {
        // Defensive no-op: a turn is only ended from EndOfTurn. A stale or duplicate
        // EndTurn (e.g. queued behind the first, which already advanced the player
        // and cleared the dice roll) refuses here rather than throwing on the now-null
        // roll below.
        if (engine.Cache.TurnState != TurnState.EndOfTurn)
            return;

        var diceRoll = engine.Cache.GetTurnDiceRoll();
        if (diceRoll == null) throw new InvalidOperationException("Turn dice roll cannot be null");
        
        var rollType = engine.Cache.Game.ModifiedDiceRollType ?? diceRoll.RollType;
        if (diceRoll.RollType != rollType)
            diceRoll = new DiceRoll(diceRoll, rollType);
        
        var player = engine.Cache.Game.CurrentPlayer();
        var extraRoll = false;
        
        if(player != null)
            extraRoll = (diceRoll.RollType is DiceRollType.Double or DiceRollType.Triple) 
                        && !diceRoll.IsDoubleFive && !player.IsInJail;

        if (extraRoll)
            await engine.TurnStateProvider.TransitionToExtraTurn(rollType == DiceRollType.Triple);
        else
            await engine.TurnStateProvider.TransitionToNextPlayer();
    }
}