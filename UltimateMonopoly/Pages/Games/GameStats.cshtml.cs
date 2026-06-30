using JC.Core.Extensions;
using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Models.Boards;
using UltimateMonopoly.Enums;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Friends;
using UltimateMonopoly.Services.Games;
using UltimateMonopoly.Services.Statistics;

namespace UltimateMonopoly.Pages.Games;

public class GameStatsModel : PageModel
{
    private readonly GameStatsService _gameStats;
    private readonly GameService _game;
    private readonly BoardCacheService _boardCache;
    private readonly ProfileService _profile;
    private readonly BlockAndReportService _blockAndReport;
    private readonly IUserInfo _userInfo;

    public GameStatsModel(GameStatsService gameStats,
        GameService game,
        BoardCacheService boardCache,
        ProfileService profile,
        BlockAndReportService blockAndReport,
        IUserInfo userInfo)
    {
        _gameStats = gameStats;
        _game = game;
        _boardCache = boardCache;
        _profile = profile;
        _blockAndReport = blockAndReport;
        _userInfo = userInfo;
    }

    public string GameId { get; private set; } = "";

    /// <summary>This game's stats for the player — the partial's model. Null until the projection runs.</summary>
    public PlayerGameStat? Stat { get; private set; }

    /// <summary>The player the stats belong to (name / avatar for the header).</summary>
    public UserProfileViewModel Player { get; private set; } = default!;

    public string GameName { get; private set; } = "";
    public string RoundingRule { get; private set; } = "";
    public Board Board { get; private set; }
    public PlayerGameOutcome? Outcome { get; private set; }

    public async Task<IActionResult> OnGetAsync(string gameId, string userId)
    {
        if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(userId))
            return NotFound();

        // Game card info comes from the game (scoped to the viewer being a player), so the header
        // renders independently of the stats — the stat row may not exist yet when the projection
        // hasn't run, in which case the page shows "Statistics not available" below.
        var game = await _game.GetFinishedGame(gameId);
        if (game is null) return NotFound();

        var gamePlayer = game.Players.FirstOrDefault(p => p.UserId == userId);
        if (gamePlayer is null) return NotFound();

        // Can't view the stats of a player blocked in either direction (viewing your own is fine —
        // a self-block can't exist). Mirrors the join-game block guard (GameSetupService).
        if (await _blockAndReport.CheckIfBlocksExist(_userInfo.UserId, [userId]))
            return NotFound();

        var player = await _profile.GetUserProfileViewModelAsync(userId);
        if (player is null) return NotFound();

        GameId = gameId;
        Player = player;
        GameName = game.Name;
        RoundingRule = game.RoundingRule.GetDescription();
        Outcome = gamePlayer.PlayerGameOutcome;
        
        var boards = await _boardCache.GetAllBoards();
        Board = boards.FirstOrDefault(b => b.BoardId == game.BoardId) 
                ?? boards.First(b => b.BoardId == null); //Default always included

        // Game/player already loaded above, so no need to include them on the stat query.
        Stat = await _gameStats.GetPlayerGameStatistics(gameId, userId, includeGame: false, includeGamePlayer: false);

        return Page();
    }
}