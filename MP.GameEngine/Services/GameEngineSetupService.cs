using System.Text.Json;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services;

public class GameEngineSetupService
{
    private readonly PlayerService _playerService;
    private readonly PropertyService _propertyService;

    public GameEngineSetupService(PlayerService playerService,
        PropertyService propertyService)
    {
        _playerService = playerService;
        _propertyService = propertyService;
    }
    
    public GameCacheModel SetupGameCache(GameDTO gameDto, Board board, List<PlayerDTO> playerDtos)
    {
        var gameModel = new GameModel
        {
            GameId = gameDto.Id,
            Metadata = new TurnMetadata
            {
                TurnNumber = 1,
                CurrentPlayerId = playerDtos.MaxBy(p => p.Dice1 + p.Dice2)?.Id
                    ?? throw new InvalidOperationException("No players in game")
            },
            Players = _playerService.GetPlayers(playerDtos),
            Properties = _propertyService.GetProperties(board),
            ReserveRuleActive = true,
            FreeParkingAmount = 0
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
        => new(gameDto, gameModel, board);
}