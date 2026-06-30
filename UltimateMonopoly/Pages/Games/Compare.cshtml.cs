using JC.Core.Extensions;
using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Models.Boards;
using UltimateMonopoly.Enums;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Friends;
using UltimateMonopoly.Services.Games;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Pages.Games;

/// <summary>
/// All-players stats comparison for one finished game (<c>/Games/Compare/{gameId}</c>). Reached
/// from the finished-game results page. Renders one column per player (seat order) over the
/// shared stat catalogue, plus per-player line graphs. Blocked players keep their stats column
/// but their identity is masked (an "Unknown" profile, either direction).
/// </summary>
public class CompareModel : PageModel
{
    private readonly GameService _game;
    private readonly GameStatsService _stats;
    private readonly PlayerCacheService _playerCache;
    private readonly BlockAndReportService _blockAndReport;
    private readonly BoardCacheService _boardCache;
    private readonly IUserInfo _userInfo;

    public CompareModel(GameService game,
        GameStatsService stats,
        PlayerCacheService playerCache,
        BlockAndReportService blockAndReport,
        BoardCacheService boardCache,
        IUserInfo userInfo)
    {
        _game = game;
        _stats = stats;
        _playerCache = playerCache;
        _blockAndReport = blockAndReport;
        _boardCache = boardCache;
        _userInfo = userInfo;
    }

    public string GameId { get; private set; } = "";
    public string GameName { get; private set; } = "";
    public string RoundingRule { get; private set; } = "";
    public string BoardName { get; private set; } = "";

    /// <summary>The default board — board-index stats resolve their space names against it.</summary>
    public Board Board { get; private set; } = default!;

    /// <summary>One column per player that has stats, in seat order.</summary>
    public List<ComparePlayer> Players { get; } = [];

    public sealed record ComparePlayer(UserProfileViewModel Profile, PlayerGameOutcome? Outcome, PlayerGameStat Stat);

    public async Task<IActionResult> OnGetAsync(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId)) return NotFound();

        // Scoped to the viewer being a player in the (finished) game.
        var game = await _game.GetFinishedGame(gameId);
        if (game is null) return NotFound();

        var stats = await _stats.GetGameStatistics(gameId, includeGame: false);

        GameId = gameId;
        GameName = game.Name;
        RoundingRule = game.RoundingRule.GetDescription();
        BoardName = string.IsNullOrEmpty(game.BoardId) ? "Default Board" : game.BoardSkin?.Name ?? "Default Board";
        var board = await _boardCache.GetAllBoards();
        Board = board.FirstOrDefault(b => b.BoardId == game.BoardId) 
                ?? board.First(b => b.BoardId == null); //Default always included

        var me = _userInfo.UserId;
        foreach (var gp in game.Players.OrderBy(p => p.OrderId))
        {
            var stat = stats.FirstOrDefault(s => s.UserId == gp.UserId);
            if (stat is null) continue;   // projection hasn't produced this player's row yet

            // Mask blocked players' identity (either direction) — stats stay, the circle/name don't.
            var masked = await _blockAndReport.CheckIfBlocksExist(me, [gp.UserId]);
            var profile = masked
                ? new UserProfileViewModel("", "Unknown", "Unknown", null, null)
                : await _playerCache.GetPlayerProfile(gp.UserId);

            Players.Add(new ComparePlayer(profile, gp.PlayerGameOutcome, stat));
        }

        return Page();
    }
}