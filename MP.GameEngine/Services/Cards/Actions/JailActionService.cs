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
    public async Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, JailAction action, CancellationToken ct, CardActionContext? context = null)
    {
        // SwapLeaveFee exchanges the holder's jail-leave cost with the context player's (the swap partner
        // an earlier Swap action in this group stashed) — "swap places with a jailed player, fees also
        // swapped". Holder-vs-context, so it sidesteps the per-target loop below.
        if (action.Kind == JailKind.SwapLeaveFee)
        {
            if (context?.ContextPlayerId is { } otherId && engine.Cache.Game.GetPlayer(otherId) is { } other)
                (player.JailCost, other.JailCost) = (other.JailCost, player.JailCost);
            return true;
        }

        var filter = action.Kind switch
        {
            JailKind.SendToJail => JailFilter.OnlyNotJailed,
            JailKind.Release => JailFilter.OnlyJailed,
            _ => JailFilter.None
        };
        
        var targets = await CardActionHelper.ResolveTargets(engine, player, action.Target, ct, filter);
        foreach (var target in targets)
        {
            switch (action.Kind)
            {
                case JailKind.SendToJail:
                    // Apply the jail-term config only if the player actually went to jail — under the
                    // JailFull event SendPlayerToJail charges the fee and returns false (not jailed),
                    // so a stale lock/rent flag must not be left on a player who's free.
                    if (await _jailService.SendPlayerToJail(engine, target, ct))
                    {
                        target.MaxJailTurnsOverride = action.TurnsOverride;
                        target.MinJailTurns = action.MinJailTurns;
                        target.CollectRentInJail = action.CollectRentInJail;
                    }
                    break;
                case JailKind.Release when target.IsInJail:
                    return await _jailService.LeaveJailByCard(engine, target, ct);
                case JailKind.ModifyLeaveFee:
                    if (action.FreeNextExit)
                        //One-shot free exit — leave JailCost (and its escalation) intact; PayJailFee waives the next charge.
                        target.FreeNextJailExit = true;
                    else if (action.LeaveFeeSetTo is { } fee)
                        target.JailCost = fee;
                    else if (action.LeaveFeeMultiplier is { } multiplier)
                        target.JailCost *= multiplier;
                    break;
            }
        }
        
        return true;
    }
}