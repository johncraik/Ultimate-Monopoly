using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a card <see cref="JailAction"/> against <see cref="JailService"/> — sending a player
/// to jail (optionally with a jail-term override) or releasing a jailed player (Get Out of Jail
/// Free). Pure board movement is the separate <see cref="MovementActionService"/>. See
/// cards-design.md §3.
/// </summary>
public class JailActionService : ICardActionService<JailAction>
{
    private readonly JailService _jailService;

    /// <summary>Creates the jail-action handler over the jail seam it routes through.</summary>
    public JailActionService(JailService jailService)
    {
        _jailService = jailService;
    }

    /// <summary>
    /// Applies the jail action to each targeted player: <see cref="JailKind.SendToJail"/> sends
    /// them to jail (applying <see cref="JailAction.TurnsOverride"/> first when present), and
    /// <see cref="JailKind.Release"/> frees a player who is currently in jail.
    /// </summary>
    /// <param name="engine">The game engine bundle the jail change mutates.</param>
    /// <param name="player">The card holder (and default target).</param>
    /// <param name="action">The jail action to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, JailAction action, CancellationToken ct)
    {
        var targets = await CardActionHelper.ResolveTargets(engine, player, action.Target, ct);
        foreach (var target in targets)
        {
            switch (action.Kind)
            {
                case JailKind.SendToJail:
                    if (action.TurnsOverride is { } turns)
                        target.MaxJailTurnsOverride = turns;
                    await _jailService.SendPlayerToJail(engine, target, ct);
                    break;
                case JailKind.Release when target.IsInJail:
                    await _jailService.LeaveJailByCard(engine, target, ct);
                    break;
            }
        }
    }
}