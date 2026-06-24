using System.Text.Json;
using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Models.ViewModels.Games;

namespace UltimateMonopoly.Areas.Admin.Pages.Games;

[Authorize(Policy = "SystemAdminOnly")]
public class DetailsModel : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly GameManagementService _games;
    private readonly AdminGameStateService _gameState;
    private readonly IUserInfo _userInfo;

    public DetailsModel(GameManagementService games, AdminGameStateService gameState, IUserInfo userInfo)
    {
        _games = games;
        _gameState = gameState;
        _userInfo = userInfo;
    }

    [BindProperty(SupportsGet = true)]
    public string GameId { get; set; } = "";

    public GameDetailViewModel Detail { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync()
    {
        var detail = await _games.GetGameDetail(GameId);
        if (detail == null) return NotFound();

        Detail = detail;
        return Page();
    }

    // ---- Downloads (combined snapshot + events) ----

    public async Task<IActionResult> OnGetDownloadTurnAsync(uint turnNumber)
    {
        var export = await _games.BuildTurnExport(GameId, turnNumber);
        if (export == null) return NotFound();
        return File(JsonSerializer.SerializeToUtf8Bytes(export, JsonOpts), "application/json",
            $"game-{GameId}-turn-{turnNumber}.json");
    }

    public async Task<IActionResult> OnGetDownloadGameAsync()
    {
        var export = await _games.BuildGameExport(GameId);
        if (export == null) return NotFound();
        return File(JsonSerializer.SerializeToUtf8Bytes(export, JsonOpts), "application/json",
            $"game-{GameId}.json");
    }

    // ---- Read-only state drawer (AJAX) ----
    // Rehydrates the chosen turn's snapshot into a read-only engine and renders the stacked read-only state
    // (player profile + game-info panel + board), all reused from the live game with IsAdminView gating.
    public async Task<IActionResult> OnGetTurnStateAsync(uint turnNumber, string? playerId)
    {
        var engine = await _gameState.BuildEngine(GameId, turnNumber);
        if (engine == null) return NotFound();

        var pid = string.IsNullOrWhiteSpace(playerId) ? engine.Cache.Game.Metadata.CurrentPlayerId : playerId;
        return Partial("_GameStateView", new GameStateViewModel(engine, pid, _userInfo.UserId));
    }

    // ---- Actions (state-gated) ----
    // The GameService call + AdminActionLog write live together in GameManagementService (so every action
    // is audited). Those methods return false until the admin-callable GameService paths land.

    private const string NotWiredMessage = "Game actions aren't wired up yet — pending admin-callable GameService paths.";

    public Task<IActionResult> OnPostDrawAsync() => ActAsync(_games.DrawGame(GameId), "Game declared a draw.");
    public Task<IActionResult> OnPostCancelAsync() => ActAsync(_games.CancelGame(GameId), "Game cancelled.");
    public Task<IActionResult> OnPostCancelDeleteAsync() => ActAsync(_games.CancelAndDeleteGame(GameId), "Game cancelled and deleted.");
    public Task<IActionResult> OnPostDeleteAsync() => ActAsync(_games.DeleteGame(GameId), "Game deleted.");
    public Task<IActionResult> OnPostForceRefreshAsync() => ActAsync(_games.ForceRefresh(GameId), "Refresh sent to the players.");

    private async Task<IActionResult> ActAsync(Task<bool> action, string success)
    {
        if (await action) TempData["Success"] = success;
        else TempData["Error"] = NotWiredMessage;
        return RedirectToPage(new { gameId = GameId });
    }
}