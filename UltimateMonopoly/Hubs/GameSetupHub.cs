using JC.Core.Models;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Hubs;

public class GameSetupHub : GameBaseHub
{
    private const string Prefix = "game-setup";

    public GameSetupHub(GameService gameService)
        : base(gameService)
    {
    }

    protected override string GroupPrefix => Prefix;

    public static string GroupName(string gameId) => GroupName(Prefix, gameId);
}