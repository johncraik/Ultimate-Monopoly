using JC.Core.Models;
using Microsoft.AspNetCore.SignalR;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Hubs;

public abstract class GameBaseHub : Hub
{
    private readonly GameService _gameService;

    public GameBaseHub(GameService gameService)
    {
        _gameService = gameService;
    }
    
    // Each hub keeps its connections in a separate group so setup and in-play
    // broadcasts never cross; derived hubs supply their own prefix.
    protected abstract string GroupPrefix { get; }

    protected static string GroupName(string prefix, string gameId) => $"{prefix}__{gameId}";

    public override async Task OnConnectedAsync()
    {
        var gameId = GetGameId();

        if (string.IsNullOrEmpty(gameId))
        {
            Context.Abort();
            return;
        }
        
        var inGame = await CheckInGame(gameId);
        if (!inGame)
        {
            Context.Abort();
            return;
        }
        
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(GroupPrefix, gameId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var gameId = GetGameId();
        if (!string.IsNullOrEmpty(gameId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(GroupPrefix, gameId));

        await base.OnDisconnectedAsync(exception);
    }

    protected string? GetGameId()
        => Context.GetHttpContext()?.Request.Query["gameId"].ToString();
    
    private async Task<bool> CheckInGame(string gameId) 
        => !string.IsNullOrEmpty(Context.UserIdentifier) 
           && await _gameService.CheckUserInGame(gameId, Context.UserIdentifier);
}