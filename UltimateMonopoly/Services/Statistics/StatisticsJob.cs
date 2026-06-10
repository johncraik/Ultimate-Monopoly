using System.Text.Json;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;
using MP.GameEngine.Services.Statistics;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.Cache;

namespace UltimateMonopoly.Services.Statistics;

public class StatisticsJob : IBackgroundJob
{
    private readonly StatisticsOrchestrator _statsOrchestrator;
    private readonly BoardCacheService _boardCacheService;
    private readonly IRepositoryManager _repos;
    private readonly ILogger<StatisticsJob> _logger;

    public StatisticsJob(StatisticsOrchestrator statsOrchestrator,
        BoardCacheService boardCacheService,
        IRepositoryManager repos,
        ILogger<StatisticsJob> logger)
    {
        _statsOrchestrator = statsOrchestrator;
        _boardCacheService = boardCacheService;
        _repos = repos;
        _logger = logger;
    }
    
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        //Get data for all finished games
        var data = await _repos.GetRepository<Game>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(g => g.State == GameState.Finished)
            .Select(g => new StatComputeData(g.Id, g.Players.Select(p => p.UserId).ToList(), g.RoundingRule))
            .ToListAsync(cancellationToken);

        var newStatRecords = await ComputeStats(data, cancellationToken);
        if (newStatRecords.Count == 0)
        {
            _logger.LogInformation("No new statistics to compute");
            return;
        }
        
        await _repos.GetRepository<PlayerGameStat>()
            .AddRangeAsync(newStatRecords, IUserInfo.SYSTEM_USER_ID, cancellationToken: cancellationToken);
    }

    
    private record StatComputeData(string GameId, List<string> PlayerIds, GameRoundingRule RoundingRule);
    
    private async Task<List<PlayerGameStat>> ComputeStats(List<StatComputeData> data,
        CancellationToken cancellationToken)
    {
        var newStatRecords = new List<PlayerGameStat>();
        foreach (var (gameId, playerIds, roundingRule) in data)
        {
            //Each Game

            var existingStats = await _repos.GetRepository<PlayerGameStat>()
                .AsQueryable()
                .Where(s => s.GameId == gameId && playerIds.Contains(s.UserId))
                .ToListAsync(cancellationToken);
            //Skip if all stats already exist
            if (existingStats.Count == playerIds.Count) continue;
            
            //Grab snapshots and events for this game
            var snapshots = await _repos.GetRepository<GameSnapshot>()
                .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
                .Where(s => s.GameId == gameId)
                .ToListAsync(cancellationToken);
            var events = await _repos.GetRepository<GameTurnEvents>()
                .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
                .Where(s => s.GameId == gameId)
                .ToListAsync(cancellationToken);

            try
            {
                //Construct list of turn snapshots from each game snapshot and their corresponding events
                var turnSnapshots = (from gameSnapshot in snapshots
                    let snapshotJson = gameSnapshot.StateJson
                    let turnEventsJson = events.FirstOrDefault(e => e.TurnId == gameSnapshot.TurnId)?.EventsJson
                    select new TurnSnapshot
                    {
                        Game = JsonSerializer.Deserialize<GameModel>(snapshotJson)
                               ?? throw new InvalidOperationException("Cannot deserialize game snapshot"),
                        // A snapshot can legitimately have no events of its own — most notably the
                        // final-turn snapshot, which captures the concluded state while that turn's
                        // events were keyed to the previous turn id. Empty/missing → empty list.
                        Events = string.IsNullOrWhiteSpace(turnEventsJson)
                            ? new List<EventReceipt>()
                            : JsonSerializer.Deserialize<List<EventReceipt>>(turnEventsJson)
                              ?? throw new InvalidOperationException("Cannot deserialize turn events")
                    })
                    // The snapshots query has no ORDER BY, so order chronologically here. The whole
                    // projection depends on it: CompleteGameSnapshot.Players is Turns[^1] (the final
                    // state passed to every service — final balance/net worth, the loan list driving
                    // outstanding debt), and the graph series are emitted in Turns order.
                    .OrderBy(t => t.Game.Metadata.TurnNumber)
                    .ToList();
                
                if(turnSnapshots.Count == 0)
                    continue;

                //Grab the default board (all stats use default board)
                var board = await _boardCacheService.GetDefaultBoard();
                
                //Compute the stats for each player in this game
                var completeGameSnapshot = new CompleteGameSnapshot
                {
                    Board = board,
                    RoundingRule = roundingRule,
                    Turns = turnSnapshots.AsReadOnly()
                };
                var computedStats = _statsOrchestrator.BuildPlayerStatRecords(completeGameSnapshot);

                //Exclude existing stats for players (only adds NEW stats for players with missing stats)
                var newStats = computedStats
                    .Where(s => !existingStats.Select(es => es.UserId).Contains(s.PlayerId))
                    .ToList();
                if (newStats.Count == 0)
                {
                    _logger.LogInformation("No new statistics to compute for game {GameId}", gameId);
                    continue;
                }
                
                _logger.LogInformation("Adding statistics for {NewStatsCount} new players in game {GameId}", newStats.Count, gameId);
                newStatRecords.AddRange(newStats.Select(s => new PlayerGameStat(gameId, s.PlayerId, s)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compute statistics for game {GameId}", gameId);
            }
        }
        
        return newStatRecords;
    }
}