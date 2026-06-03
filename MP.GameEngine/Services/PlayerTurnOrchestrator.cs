using MP.GameEngine.Enums;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
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

    public PlayerTurnOrchestrator(DiceService diceService,
        TransactionService transactionService,
        MovementService movementService,
        PlayerService playerService,
        JailService jailService,
        BoardService boardService)
    {
        _diceService = diceService;
        _transactionService = transactionService;
        _movementService = movementService;
        _playerService = playerService;
        _jailService = jailService;
        _boardService = boardService;
    }


    public async Task StartPlayerTurn(Framework.GameEngine engine, CancellationToken ct)
    {
        // Defensive no-op: a turn is only kicked off from StartOfTurn. A stale or
        // duplicate StartTurn (e.g. a second tap that queued behind the first)
        // refuses here rather than rolling into the engine and throwing — an
        // illegal mutation no-ops, it does not fault the turn. Mirrors the guard
        // in ResolveThirdDieMovement.
        if (engine.Cache.TurnState != TurnState.StartOfTurn)
            return;
        
        //Roll dice
        var dice = await _diceService.RollTurnDice(engine, ct);
        
        //Start roll movement phase for player
        engine.TurnStateProvider.TransitionToRollMovementPhase();

        var player = engine.Cache.Game.CurrentPlayer();
        if (player == null) throw new InvalidOperationException("Current player not found in game players list.");
        
        //Existing player is not included, and list is ordered by clockwise from player POV by default
        var otherPlayers = engine.Cache.Game.GetPlayers();

        var playerIdWithMatchingDiceNum = engine.Cache.Game.CheckAnyDiceNumbers(dice);
        if(!string.IsNullOrEmpty(playerIdWithMatchingDiceNum))
            await _playerService.ResolveDiceNumber(engine, playerIdWithMatchingDiceNum, ct);

        if (player.IsInJail)
            player.JailTurnCounter++;

        ushort movement;
        var transitionToThirdDie = true;
        switch (dice.RollType)
        {
            case DiceRollType.Normal:
                player.DoublesInRow = 0;
                player.TriplesInRow = 0;

                if (!player.IsInJail)
                {
                    //Standard roll, move them normally:
                    movement = (ushort)(dice.Die1 + (dice.Die2 ?? throw new InvalidOperationException("Second die cannot be null")));
                    await _movementService.MovePlayer(engine, player, movement, ct);
                    await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
                }
                else if(player.JailTurnCounter == (player.MaxJailTurnsOverride ?? RuleDictionary.MaxJailTurns))
                {
                    //Jail counter already increased before role type switch
                    await _jailService.ForcePlayerToLeaveJail(engine, player, ct);
                }
                
                break;
            
            case DiceRollType.Double:
                if (player.DoublesInRow < RuleDictionary.DoublesBeforeJail)
                {
                    //TODO: Take a double card!!!
                    
                    //Will move player OUT of jail if in jail
                    await _jailService.CheckAndLeaveJail(engine, player, ct);
                    
                    //Get the double effect record, and notify player
                    var effect = DoubleEffects.For(dice.Die1);
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, effect.Title, effect.Body, ct: ct);
                    
                    //Apply snake eyes bonus (if applicable)
                    if(effect.SnakeEyesBonus)
                        await _transactionService.ReceiveSnakeEyes(engine, player, ct);
                    
                    //Move the player based on the effect steps:
                    if(effect.RollerSteps.Count > 0)
                        foreach (var step in effect.RollerSteps)
                        {
                            await _movementService.MovePlayer(engine, player, step, ct);
                            await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
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
                            }
                        
                        //Increment other player's miss turns if effect requires it'
                        if(effect.OtherPlayerMissesTurn)
                            p.TurnsToMiss++;
                    }
                    
                    player.FlipDirection();
                }
                else
                {
                    //Too many doubles in a row, so send player to jail:
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Going to Jail!", "You have rolled too many doubles in a row.", ct: ct);
                    await _jailService.SendPlayerToJail(engine, player, ct);
                }
                
                break;
            
            case DiceRollType.Triple:
                if (player.TriplesInRow < RuleDictionary.TriplesBeforeJail)
                {
                    //TODO: Take a triple card!!!
                    
                    //Will move player OUT of jail if in jail
                    await _jailService.CheckAndLeaveJail(engine, player, ct);
                    
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Triple!", "You rolled a triple, you will move the combined total of all three dice.", ct: ct);
                    
                    movement = (ushort)((dice.Die1 + dice.Die2 + dice.ThirdDie) ?? throw new InvalidOperationException("Dice roll result cannot be null"));
                    await _movementService.MovePlayer(engine, player, movement, ct);
                    await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
                }
                else
                {
                    //Too many triples in a row, so send player to jail:
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Going to Jail!", "You have rolled too many triples in a row.", ct: ct);
                    await _jailService.SendPlayerToJail(engine, player, ct);
                }

                transitionToThirdDie = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if(player.InitialRoll && player.BoardIndex != IndexHelper.GoSpace)
            player.InitialRoll = false;
        
        if(transitionToThirdDie)
            engine.TurnStateProvider.TransitionToThirdDie();
        else
            //Triple doesnt grant third die movement
            engine.TurnStateProvider.TransitionToEndOfTurn();
    }

    


    public async Task ResolveThirdDieMovement(Framework.GameEngine engine, CancellationToken ct)
    {
        if(engine.Cache.TurnState != TurnState.ThirdDieMovement)
            return;
        
        var otherPlayers = engine.Cache.Game.GetPlayers();
        var thirdDie = engine.Cache.TurnDiceRoll?.ThirdDie ?? throw new InvalidOperationException("Third die roll cannot be null");

        foreach (var player in otherPlayers.Where(player => !player.IsInJail))
        {
            //Move each player based on third die, if not in jail
            await _movementService.MovePlayer(engine, player, thirdDie, ct);
            await _boardService.ResolveBoardSpaceForPlayer(engine, player, ct);
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

        var diceRoll = engine.Cache.TurnDiceRoll;
        if (diceRoll == null) throw new InvalidOperationException("Turn dice roll cannot be null");
        
        var player = engine.Cache.Game.CurrentPlayer();
        var extraRoll = false;
        
        if(player != null)
            extraRoll = (diceRoll.RollType is DiceRollType.Double or DiceRollType.Triple) 
                        && !diceRoll.IsDoubleFive && !player.IsInJail;

        if (extraRoll)
            await engine.TurnStateProvider.TransitionToExtraTurn(diceRoll.RollType == DiceRollType.Triple);
        else
            await engine.TurnStateProvider.TransitionToNextPlayer();
    }
}