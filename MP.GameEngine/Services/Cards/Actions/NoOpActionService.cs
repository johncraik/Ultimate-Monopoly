using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.Cards.Actions;

/// <summary>
/// Resolves a <see cref="NoOpAction"/> — does nothing and always succeeds. The card's effect is its
/// card-level <c>SuppressDefault</c> metadata, applied by the trigger layer at the firing point, not
/// by this action. See cards-design.md §3.
/// </summary>
public sealed class NoOpActionService : ICardActionService<NoOpAction>
{
    /// <summary>No-op — the effect lives in the card's <c>SuppressDefault</c>, not in an action.</summary>
    public Task<bool> ResolveActionAsync(Framework.GameEngine engine, PlayerModel player, NoOpAction action,
        CancellationToken ct, CardActionContext? context = null)
        => Task.FromResult(true);
}