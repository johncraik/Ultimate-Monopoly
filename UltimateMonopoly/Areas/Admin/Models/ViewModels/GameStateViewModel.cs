using MP.GameEngine.Services.Framework;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels;

/// <summary>Backs the admin read-only game-state drawer (_GameStateView): the rehydrated engine, the
/// selected player, and the viewer id (the acting admin) for the reused in-play partials.</summary>
public record GameStateViewModel(GameEngine Engine, string PlayerId, string ViewerUserId);