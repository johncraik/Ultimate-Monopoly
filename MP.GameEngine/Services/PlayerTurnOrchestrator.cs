using MP.GameEngine.Enums;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services;

public class PlayerTurnOrchestrator
{
    private readonly DiceService _diceService;
    private readonly MovementService _movementService;
    private readonly PlayerService _playerService;

    public PlayerTurnOrchestrator(DiceService diceService,
        MovementService movementService,
        PlayerService playerService)
    {
        _diceService = diceService;
        _movementService = movementService;
        _playerService = playerService;
    }


    public async Task StartPlayerTurn(Framework.GameEngine engine, CancellationToken ct)
    {
        var player = engine.Cache.Game.CurrentPlayer();
        if (player == null) throw new InvalidOperationException("Current player not found in game players list.");
        
        //Existing player is not included, and list is ordered by clockwise from player POV by default
        var otherPlayers = engine.Cache.Game.GetPlayers();

        ushort movement;
        var dice = await _diceService.RollTurnDice(engine, ct);
        var playerIdWithMatchingDiceNum = engine.Cache.Game.CheckAnyDiceNumbers(dice);
        if(!string.IsNullOrEmpty(playerIdWithMatchingDiceNum))
            await _playerService.ResolveDiceNumber(engine, playerIdWithMatchingDiceNum, ct);
        
        switch (dice.RollType)
        {
            case DiceRollType.Normal:
                player.DoublesInRow = 0;
                player.TriplesInRow = 0;
                
                //Standard roll, move them normally:
                movement = (ushort)(dice.Die1 + (dice.Die2 ?? throw new InvalidOperationException("Second die cannot be null")));
                await _movementService.MovePlayer(engine, player, movement, ct);
                
                engine.TurnStateProvider.TransitionToThirdDie();
                break;
            case DiceRollType.Double:
                if (player.DoublesInRow < RuleDictionary.DoublesBeforeJail)
                {
                    //Get the double effect record, and notify player
                    var effect = DoubleEffects.For(dice.Die1);
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, effect.Title, effect.Body, ct: ct);
                    
                    //Move the player based on the effect steps:
                    if(effect.RollerSteps.Count > 0)
                        foreach (var step in effect.RollerSteps)
                        {
                            await _movementService.MovePlayer(engine, player, step, ct);
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
                    await SendToJail(engine, player, ct);
                }
                
                engine.TurnStateProvider.TransitionToThirdDie();
                break;
            case DiceRollType.Triple:
                if (player.TriplesInRow < RuleDictionary.TriplesBeforeJail)
                {
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Triple!", "You rolled a triple, you will move the combined total of all three dice.", ct: ct);
                    
                    movement = (ushort)((dice.Die1 + dice.Die2 + dice.ThirdDie) ?? throw new InvalidOperationException("Dice roll result cannot be null"));
                    await _movementService.MovePlayer(engine, player, movement, ct);
                    engine.TurnStateProvider.TransitionToEndOfTurn();
                }
                else
                {
                    //Too many triples in a row, so send player to jail:
                    _ = await engine.PromptProvider.Acknowledge(player.PlayerId, "Going to Jail!", "You have rolled too many triples in a row.", ct: ct);
                    await SendToJail(engine, player, ct);
                }
                
                engine.TurnStateProvider.TransitionToEndOfTurn();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task SendToJail(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //Going to jail
        //Reset counters, and send player to jail:
        player.DoublesInRow = 0;
        player.TriplesInRow = 0;
        await _movementService.SendPlayerToJail(engine, player, ct);
    }
}