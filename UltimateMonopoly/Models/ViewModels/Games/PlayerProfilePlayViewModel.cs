using MP.GameEngine.Services.Framework;

namespace UltimateMonopoly.Models.ViewModels.Games;

/// <summary>
/// Model for <c>_PlayerProfileView</c> — the in-game player profile (phone view,
/// and later the host's drawer). It renders entirely from the engine cache
/// (<see cref="Engine"/>), exactly as the host <c>_PlayView</c> does, so the
/// first-load and live (StateChanged) render paths are identical.
/// </summary>
/// <param name="Engine">The live game engine bundle — the single source the view reads from.</param>
/// <param name="PlayerId">The profiled player (their <see cref="MP.GameEngine.Models.Snapshot.PlayerModel.PlayerId"/>, which is the user id).</param>
/// <param name="ViewerUserId">
/// The signed-in user viewing the profile (self or the host). Used only to gate
/// the command buttons via the host-bypass-aware <c>TurnStateProvider.Can…</c>
/// checks — never to choose what data is shown.
/// </param>
public record PlayerProfilePlayViewModel(GameEngine Engine, string PlayerId, string ViewerUserId);