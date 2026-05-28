using JC.Core.Extensions;
using JC.Core.Models;
using JC.Web.UI.Helpers;
using JC.Web.UI.HTML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Models.ViewModels;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Areas.Game.Pages;

[Authorize]
public class SetupModel : PageModel
{
    private readonly GameSetupService _gameSetup;
    private readonly ProfileService _profiles;
    private readonly UrlLinkService _urlLinks;
    private readonly IUserInfo _userInfo;

    public SetupModel(GameSetupService gameSetup,
        ProfileService profiles,
        UrlLinkService urlLinks,
        IUserInfo userInfo)
    {
        _gameSetup = gameSetup;
        _profiles = profiles;
        _urlLinks = urlLinks;
        _userInfo = userInfo;
    }

    public string GameId { get; private set; } = string.Empty;
    public string GameName { get; private set; } = string.Empty;
    public string RoundingRuleText { get; private set; } = string.Empty;
    public string BoardName { get; private set; } = "Default board";
    public string JoinQrSvg { get; private set; } = string.Empty;
    public string JoinCode { get; private set; } = string.Empty;
    public List<PlayerCard> Players { get; private set; } = [];

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public AlertType StatusAlertType => StatusKind == "danger" ? AlertType.Error : AlertType.Success;

    public async Task<IActionResult> OnGetAsync(string id)
        => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnGetPlayerCardAsync(string id, string userId)
    {
        var game = await _gameSetup.GetSetupGame(id);
        if (game is null) return NotFound();

        var player = game.Players.FirstOrDefault(p => !p.IsDeleted && p.UserId == userId);
        if (player is null) return NotFound();

        var card = await BuildCard(player, game.CreatedById);
        return card is null ? NotFound() : Partial("_PlayerCard", card);
    }

    public async Task<IActionResult> OnPostKickAsync(string id, string targetUserId)
    {
        var ok = await _gameSetup.TryKickPlayerFromGame(id, targetUserId);
        SetStatus(ok, "Player removed from the game.", "Could not remove that player.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSetDiceAsync(string id, string targetUserId, ushort dice1, ushort dice2)
    {
        // The host may set anyone's numbers; a player may only set their own.
        if (_userInfo.UserId != targetUserId && !await ViewerIsHostOf(id))
            return Forbid();

        var ok = await _gameSetup.TrySetPlayerDiceNumbers(id, targetUserId, dice1, dice2);
        SetStatus(ok, "Dice numbers updated.", "Could not set those dice numbers — they may already be taken.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReorderAsync(string id, List<string>? orderedUserIds)
    {
        if (!await ViewerIsHostOf(id))
            return Forbid();

        var ok = await _gameSetup.TryReorderPlayers(id, orderedUserIds ?? []);
        SetStatus(ok, "Seat order updated.", "Could not update the seat order.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStartAsync(string id)
    {
        if (!await ViewerIsHostOf(id))
            return Forbid();

        var ok = await _gameSetup.TryStartGame(id);
        if (ok) return Redirect($"/Game/Play/{id}");

        SetStatus(false, string.Empty, "Could not start the game.");
        return RedirectToPage(new { id });
    }

    private async Task<bool> ViewerIsHostOf(string gameId)
    {
        var game = await _gameSetup.GetSetupGame(gameId);
        return game is not null && game.CreatedById == _userInfo.UserId;
    }

    private void SetStatus(bool ok, string success, string failure)
    {
        StatusMessage = ok ? success : failure;
        StatusKind = ok ? "success" : "danger";
    }

    private async Task<bool> LoadAsync(string id)
    {
        var game = await _gameSetup.GetSetupGame(id);
        if (game is null) return false;

        GameId = game.Id;
        GameName = game.Name;
        RoundingRuleText = game.RoundingRule.GetDescription();
        BoardName = game.BoardSkin?.Name ?? "Default board";
        JoinCode = game.JoinCode;

        var players = game.Players
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.OrderId)
            .ToList();

        foreach (var p in players)
        {
            var card = await BuildCard(p, game.CreatedById);
            if (card is not null) Players.Add(card);
        }

        var joinLink = _urlLinks.GetUrlLink($"/Game/Setup/{game.Id}");
        JoinQrSvg = new QrCodeHelper(QrCodeFormat.Svg, 10).GenerateQrCode(joinLink);

        return true;
    }

    private async Task<PlayerCard?> BuildCard(GamePlayer player, string hostUserId)
    {
        var profile = await _profiles.GetUserProfileViewModelAsync(player.UserId);
        return profile is null
            ? null
            : new PlayerCard(player.OrderId, profile, player.Dice1, player.Dice2, player.UserId == hostUserId);
    }
}