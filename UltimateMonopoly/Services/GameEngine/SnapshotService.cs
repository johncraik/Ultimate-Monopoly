using System.Text.Json;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using JC.Identity.Models;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using UltimateMonopoly.Models.DataModels.Games;

namespace UltimateMonopoly.Services.GameEngine;

public class SnapshotService : ISnapshotService
{
    private readonly IRepositoryManager _repos;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(IRepositoryManager repos,
        ILogger<SnapshotService> logger)
    {
        _repos = repos;
        _logger = logger;
    }

    public async Task CreateSnapshotAsync(GameModel game, bool completeTransaction = true, bool finalTurn = false)
    {
        var turn = new GameTurn(game.GameId, game.Metadata.CurrentPlayerId)
        {
            TurnNumber = game.Metadata.TurnNumber
        };
        
        if(finalTurn)
            turn.SetFinalTurn();

        game.Metadata.CurrentTurnId = turn.Id;
        var snapshot = new GameSnapshot(turn.Id, game.GameId);

        var stateJson = JsonSerializer.Serialize(game);
        snapshot.StateJson = stateJson;
        
        if(completeTransaction)
            await _repos.BeginTransactionAsync();
        try
        {
            await _repos.GetRepository<GameTurn>()
                .AddAsync(turn, userId: IUserInfo.SYSTEM_USER_ID, saveNow: false);
            
            await _repos.GetRepository<GameSnapshot>()
                .AddAsync(snapshot, userId: IUserInfo.SYSTEM_USER_ID, saveNow: false);

            if (!completeTransaction) return;
            
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            if (completeTransaction)
                await _repos.RollbackTransactionAsync();
            
            _logger.LogError(ex, "Error persisting snapshot for game {GameId}", snapshot.GameId);
            throw;
        }
    }

    public async Task CreateTurnEventSnapshotAsync(string gameId, string turnId, List<EventReceipt> receipts)
    {
        var turn = await _repos.GetRepository<GameTurn>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(t => t.Id == turnId && t.GameId == gameId);
        if(turn == null)
            throw new InvalidOperationException("Turn not found");
        
        var eventJson = JsonSerializer.Serialize(receipts);
        var turnEvent = new GameTurnEvents(turnId, gameId, eventJson);
        
        await _repos.GetRepository<GameTurnEvents>()
            .AddAsync(turnEvent, userId: IUserInfo.SYSTEM_USER_ID);
    }
}