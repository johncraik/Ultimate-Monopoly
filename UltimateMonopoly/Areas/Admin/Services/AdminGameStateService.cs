using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Services;
using MP.GameEngine.Services.Cards;
using MP.GameEngine.Services.Framework;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.Cache;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Rehydrates a game's stored snapshot for a given turn into a <b>read-only</b> <see cref="GameEngine"/> —
/// the same engine the live in-play partials render from, but with no pump, no SignalR, and no persistence.
/// It deserialises the snapshot via <see cref="GameEngineSetupService.SetupGameCache(GameDTO, string, MP.GameEngine.Models.Boards.Board)"/>
/// and wraps it in a fresh <see cref="GameEngine"/>; it is never populated into the live
/// <see cref="GameCacheService"/> nor saved. The admin page sets <c>ViewData["AdminId"]</c> so the reused
/// <c>_PlayerProfileView</c> force-disables every command button.
/// </summary>
public class AdminGameStateService
{
    private readonly IRepositoryManager _repos;
    private readonly GameCacheService _gameCache;
    private readonly GameEngineSetupService _engineSetup;
    private readonly ISnapshotService _snapshotService;
    private readonly IEngineNotifier _notifier;
    private readonly IShortfallService _shortfallService;
    private readonly CardService _cardService;

    public AdminGameStateService(IRepositoryManager repos,
        GameCacheService gameCache,
        GameEngineSetupService engineSetup,
        ISnapshotService snapshotService,
        IEngineNotifier notifier,
        IShortfallService shortfallService,
        CardService cardService)
    {
        _repos = repos;
        _gameCache = gameCache;
        _engineSetup = engineSetup;
        _snapshotService = snapshotService;
        _notifier = notifier;
        _shortfallService = shortfallService;
        _cardService = cardService;
    }

    /// <summary>Builds a read-only engine over the given turn's snapshot, or null if the game / turn / snapshot is missing.</summary>
    public async Task<GameEngine?> BuildEngine(string gameId, uint turnNumber)
    {
        var game = await _repos.GetRepository<UltimateMonopoly.Models.DataModels.Games.Game>()
            .AsQueryable().AsNoTracking()
            .FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return null;

        var turn = await _repos.GetRepository<GameTurn>()
            .AsQueryable().AsNoTracking()
            .FirstOrDefaultAsync(t => t.GameId == gameId && t.TurnNumber == turnNumber);
        if (turn == null) return null;

        var snapshot = await _repos.GetRepository<GameSnapshot>()
            .AsQueryable().AsNoTracking()
            .FirstOrDefaultAsync(s => s.TurnId == turn.Id);
        if (snapshot == null) return null;

        var board = await _gameCache.GetGameBoard(game.UserId, game.BoardId);
        if (board == null) return null;

        var gameDto = new GameDTO(game.Id, game.Name, game.BoardId, game.RoundingRule,
            game.UserId, game.State, game.Outcome);

        var cache = _engineSetup.SetupGameCache(gameDto, snapshot.StateJson, board);
        return new GameEngine(cache, _snapshotService, _notifier, _shortfallService, _cardService);
    }
}