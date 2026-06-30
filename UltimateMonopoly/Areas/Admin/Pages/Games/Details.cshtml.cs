using System.Text.Json;
using JC.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;
using UltimateMonopoly.Areas.Admin.Services;

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
    public string GameId { get; set; } = "";

    public GameDetailViewModel Detail { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(string gameId)
    {
        GameId = gameId;
        
        var detail = await _games.GetGameDetail(GameId);
        if (detail == null) return NotFound();

        Detail = detail;
        return Page();
    }

    // ---- Downloads (combined snapshot + events) ----

    public async Task<IActionResult> OnGetDownloadTurnAsync(string gameId, uint turnNumber)
    {
        GameId = gameId;
        
        var export = await _games.BuildTurnExport(GameId, turnNumber);
        if (export == null) return NotFound();
        return File(JsonSerializer.SerializeToUtf8Bytes(export, JsonOpts), "application/json",
            $"game-{GameId}-turn-{turnNumber}.json");
    }

    public async Task<IActionResult> OnGetDownloadGameAsync(string gameId)
    {
        GameId = gameId;
        
        var export = await _games.BuildGameExport(GameId);
        if (export == null) return NotFound();
        return File(JsonSerializer.SerializeToUtf8Bytes(export, JsonOpts), "application/json",
            $"game-{GameId}.json");
    }

    // ---- Read-only state drawer (AJAX) ----
    // Rehydrates the chosen turn's snapshot into a read-only engine and renders the stacked read-only state
    // (player profile + game-info panel + board), all reused from the live game with IsAdminView gating.
    public async Task<IActionResult> OnGetTurnStateAsync(string gameId, uint turnNumber, string? playerId)
    {
        GameId = gameId;
        
        var engine = await _gameState.BuildEngine(GameId, turnNumber);
        if (engine == null) return NotFound();

        var pid = string.IsNullOrWhiteSpace(playerId) ? engine.Cache.Game.Metadata.CurrentPlayerId : playerId;
        return Partial("_GameStateView", new GameStateViewModel(engine, pid, _userInfo.UserId));
    }

    // ---- Actions (state-gated) ----
    // The GameService call + AdminActionLog write live together in GameManagementService (so every action
    // is audited). A false return means the action couldn't run (wrong state / game already gone).
    // Draw / Cancel / Force-refresh leave the game in place, so they redirect back here; Delete and
    // Cancel+Delete remove it, so they redirect to the list.

    private const string ActionFailedMessage = "That action couldn't be completed — the game may have changed state or already been removed.";

    public async Task<IActionResult> OnPostDrawAsync(string gameId)
    {
        GameId = gameId;
        
        if (await _games.DrawGame(GameId)) TempData["Success"] = "Game declared a draw.";
        else TempData["Error"] = ActionFailedMessage;
        return RedirectToPage(new { gameId = GameId });
    }

    public async Task<IActionResult> OnPostCancelAsync(string gameId)
    {
        GameId = gameId;
        
        if (await _games.CancelGame(GameId)) TempData["Success"] = "Game cancelled.";
        else TempData["Error"] = ActionFailedMessage;
        return RedirectToPage(new { gameId = GameId });
    }

    public async Task<IActionResult> OnPostForceRefreshAsync(string gameId)
    {
        GameId = gameId;
        
        await _games.ForceRefresh(GameId);
        TempData["Success"] = "Refresh sent to the players.";
        return RedirectToPage(new { gameId = GameId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string gameId)
    {
        GameId = gameId;
        
        if (await _games.DeleteGame(GameId))
        {
            TempData["Success"] = "Game deleted.";
            return RedirectToPage("./Index");
        }
        TempData["Error"] = ActionFailedMessage;
        return RedirectToPage(new { gameId = GameId });
    }

    // Irreversible — reverts to the turn selected in the state dropdown, hard-deleting every later turn.
    public async Task<IActionResult> OnPostRevertToTurnAsync(string gameId, uint turnNumber)
    {
        GameId = gameId;

        if (await _games.RevertGameToTurn(GameId, turnNumber))
            TempData["Success"] = $"Game reverted to turn {turnNumber} — all later turns were permanently deleted.";
        else
            TempData["Error"] = "Couldn't revert — the selected turn is already the latest, or the game no longer exists.";

        return RedirectToPage(new { gameId = GameId });
    }

    public async Task<IActionResult> OnPostCancelDeleteAsync(string gameId)
    {
        GameId = gameId;

        var (cancelled, deleted) = await _games.CancelAndDeleteGame(GameId);
        if (deleted)
        {
            TempData["Success"] = "Game cancelled and deleted.";
            return RedirectToPage("./Index");
        }
        // Cancel succeeded but the delete didn't — the game is now Cancelled, still here.
        TempData["Error"] = cancelled
            ? "Game was cancelled, but it couldn't be deleted."
            : ActionFailedMessage;
        return RedirectToPage(new { gameId = GameId });
    }
}