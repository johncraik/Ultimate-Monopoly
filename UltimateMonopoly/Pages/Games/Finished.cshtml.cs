using JC.Core.Extensions;
using JC.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Enums.Games;
using UltimateMonopoly.Enums;
using UltimateMonopoly.Models.ViewModels;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Friends;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Pages.Games;

public class FinishedModel : PageModel
{
    private readonly GameService _game;
    private readonly ProfileService _profile;
    private readonly BlockAndReportService _blockAndReport;
    private readonly IUserInfo _userInfo;

    public FinishedModel(GameService game, ProfileService profile,
        BlockAndReportService blockAndReport, IUserInfo userInfo)
    {
        _game = game;
        _profile = profile;
        _blockAndReport = blockAndReport;
        _userInfo = userInfo;
    }

    public string GameId { get; private set; } = "";
    public string CurrentUserId { get; private set; } = "";
    public string GameName { get; private set; } = "";
    public string RoundingRule { get; private set; } = "";
    public string BoardName { get; private set; } = "";

    public bool IsDraw { get; private set; }
    public PlayerCard? Winner { get; private set; }
    public List<PlayerCard> DrawnPlayers { get; private set; } = [];
    public List<PlayerCard> Losers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId)) return NotFound();

        // Scoped to the current user (must have been a player) + Finished — null otherwise.
        var game = await _game.GetFinishedGame(gameId);
        if (game is null) return NotFound();

        GameId = gameId;
        CurrentUserId = _userInfo.UserId;
        GameName = game.Name;
        RoundingRule = game.RoundingRule.GetDescription();
        BoardName = string.IsNullOrEmpty(game.BoardId) ? "Default Board" : game.BoardSkin?.Name ?? "Default Board";
        IsDraw = game.Outcome == GameOutcome.Drawn;

        foreach (var player in game.Players.OrderBy(p => p.OrderId))
        {
            // Mask blocked players' identity (either direction), as on the comparison page —
            // their card keeps its result/outcome, but the profile shows "Unknown".
            UserProfileViewModel? profile;
            if (await _blockAndReport.CheckIfBlocksExist(CurrentUserId, [player.UserId]))
            {
                profile = new UserProfileViewModel("", "Unknown", "Unknown", null, null);
            }
            else
            {
                profile = await _profile.GetUserProfileViewModelAsync(player.UserId);
                if (profile is null) continue;
            }

            // Reuse the setup player card (IsSetup: false → renders the "View" button); GameId set
            // so that button opens this player's game stats rather than their social profile.
            var card = new PlayerCard(player.OrderId, profile, player.Dice1, player.Dice2,
                IsHost: game.CreatedById == player.UserId, IsSetup: false, GameId: gameId);

            switch (player.PlayerGameOutcome)
            {
                case PlayerGameOutcome.Winner:
                    Winner = card;
                    break;
                case PlayerGameOutcome.Drawn:
                    DrawnPlayers.Add(card);
                    break;
                case PlayerGameOutcome.Loser:
                    Losers.Add(card);
                    break;
            }
        }

        return Page();
    }
}