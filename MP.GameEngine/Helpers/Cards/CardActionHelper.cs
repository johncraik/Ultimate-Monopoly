using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Helpers.Cards;

/// <summary>
/// Shared helpers for the per-action card services — logic common to more than one
/// <see cref="Abstractions.Cards.ICardActionService{T}"/> implementation.
/// </summary>
public static class CardActionHelper
{
    /// <summary>
    /// Resolves a <see cref="PlayerTarget"/> to the players an action acts on:
    /// <see cref="PlayerTarget.Self"/> → the holder; <see cref="PlayerTarget.AllOthers"/> →
    /// every active other player (clockwise from the holder); <see cref="PlayerTarget.ChosenPlayer"/>
    /// → a single player the holder picks via a <see cref="TargetPlayerPrompt"/>.
    /// </summary>
    /// <param name="engine">The game engine bundle (for the player list and the prompt).</param>
    /// <param name="holder">The card holder choosing/being targeted.</param>
    /// <param name="target">Which players the action targets.</param>
    /// <param name="jailFilter">Whether to filter players based on jail status</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The resolved players. Empty when a <see cref="PlayerTarget.ChosenPlayer"/> has no eligible
    /// candidates (e.g. the holder is the only active player).
    /// </returns>
    public static async Task<List<PlayerModel>> ResolveTargets(Services.Framework.GameEngine engine, PlayerModel holder,
        PlayerTarget target, CancellationToken ct, JailFilter jailFilter = JailFilter.None)
    {
        switch (target)
        {
            case PlayerTarget.Self:
                return [holder];
            case PlayerTarget.AllOthers:
                var players = engine.Cache.Game.GetPlayers(holder.PlayerId);
                return FilterJailed(players, jailFilter); 
            case PlayerTarget.Everyone:
                // The holder first, then every other active player (clockwise from the holder).
                var everyone = engine.Cache.Game.GetPlayers(holder.PlayerId, excludePovPlayer: false);
                return FilterJailed(everyone, jailFilter);
            case PlayerTarget.ChosenPlayer:
                var others = engine.Cache.Game.GetPlayers(holder.PlayerId);
                others = FilterJailed(others, jailFilter);
                
                if (others.Count == 0)
                    return [];

                var response = await engine.PromptProvider.RequestAsync(new TargetPlayerPrompt
                {
                    PlayerId = holder.PlayerId,
                    Title = "Choose a Player",
                    Body = "Select a player.",
                    EligiblePlayerIds = others.Select(p => p.PlayerId).ToList(),
                    Count = 1
                }, ct);

                return response.SelectedPlayerIds
                    .Select(id => engine.Cache.Game.GetPlayer(id))
                    .Where(p => p is not null)
                    .ToList()!;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }
    
    private static List<PlayerModel> FilterJailed(List<PlayerModel> players, JailFilter filter)
        => filter switch
        {
            JailFilter.OnlyJailed => players.Where(p => p.IsInJail).ToList(),
            JailFilter.OnlyNotJailed => players.Where(p => !p.IsInJail).ToList(),
            _ => players
        };
}