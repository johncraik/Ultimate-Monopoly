using JC.Core.Extensions;
using JC.Core.Models;
using JC.Web.UI.Helpers;
using JC.Web.UI.HTML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Identity.Services;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Games;

namespace UltimateMonopoly.Pages.Game;

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

    public record PlayerCard(ushort OrderId, UserProfileViewModel Profile, ushort? Dice1, ushort? Dice2, bool IsHost);

    public string GameId { get; private set; } = string.Empty;
    public string GameName { get; private set; } = string.Empty;
    public string RoundingRuleText { get; private set; } = string.Empty;
    public string BoardName { get; private set; } = "Default board";
    public bool ViewerIsHost { get; private set; }
    public string JoinQrSvg { get; private set; } = string.Empty;
    public string JoinCode { get; private set; } = string.Empty;
    public List<PlayerCard> Players { get; private set; } = [];

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public AlertType StatusAlertType => StatusKind == "danger" ? AlertType.Error : AlertType.Success;

    public async Task<IActionResult> OnGetAsync(string id)
        => await LoadAsync(id) ? Page() : NotFound();

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
        ViewerIsHost = game.CreatedById == _userInfo.UserId;

        var players = game.Players
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.OrderId)
            .ToList();

        foreach (var p in players)
        {
            var profile = await _profiles.GetUserProfileViewModelAsync(p.UserId);
            if (profile is null) continue;
            Players.Add(new PlayerCard(p.OrderId, profile, p.Dice1, p.Dice2, p.UserId == game.CreatedById));
        }

        var joinLink = _urlLinks.GetUrlLink($"/Game/Setup/{game.Id}");
        JoinQrSvg = new QrCodeHelper(QrCodeFormat.Svg, 10).GenerateQrCode(joinLink);

        return true;
    }
}