using System.Text.Json;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services;

public class GameSetupService
{
    private readonly PlayerService _playerService;
    private readonly PropertyService _propertyService;

    public GameSetupService(PlayerService playerService,
        PropertyService propertyService)
    {
        _playerService = playerService;
        _propertyService = propertyService;
    }
    
    public GameCacheModel SetupGameCache(GameDTO gameDto, GameTurnDTO turnDto, Board board, List<PlayerDTO> playerDtos)
    {
        var gameModel = new GameModel
        {
            Metadata = new TurnMetadata
            {
                CurrentTurnId = turnDto.Id,
                CurrentPlayerId = turnDto.PlayerId,
                TurnNumber = turnDto.TurnNumber
            },
            Players = _playerService.GetPlayers(playerDtos),
            Properties = _propertyService.GetProperties(board)
        };

        //TODO Load cards!
        
        return SetupGameCache(gameDto, gameModel, board);
    }

    public GameCacheModel SetupGameCache(GameDTO gameDto, string snapshotJson, Board board)
    {
        var gameModel = JsonSerializer.Deserialize<GameModel>(snapshotJson);
        return gameModel == null 
            ? throw new ArgumentException("Invalid game snapshot JSON") 
            : SetupGameCache(gameDto, gameModel, board);
    }
    
    private GameCacheModel SetupGameCache(GameDTO gameDto, GameModel gameModel, Board board)
        => new GameCacheModel(gameDto, gameModel, board);
}