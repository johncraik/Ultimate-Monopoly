using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Abstractions.Cards;

/// <summary>
/// Handler for one concrete <see cref="CardAction"/> type — the per-action seam
/// <see cref="Services.Cards.CardService"/> dispatches to. One implementation per action type
/// (Money / Movement / Jail), each owning that type's behaviour in isolation so the card
/// interpreter stays a thin orchestrator (cards-design.md §3).
/// </summary>
/// <typeparam name="T">The concrete <see cref="CardAction"/> this service resolves.</typeparam>
public interface ICardActionService<T>
    where T : CardAction
{
    /// <summary>
    /// Applies <paramref name="action"/> for <paramref name="player"/> against the engine —
    /// the action's effect (money movement, board movement, jail entry/exit, …). Resolves any
    /// follow-on targeting prompts the action needs.
    /// </summary>
    /// <param name="engine">The game engine bundle the action mutates.</param>
    /// <param name="player">The card holder the action is resolved for.</param>
    /// <param name="action">The action to apply.</param>
    /// <param name="ct">Cancellation token, tripped on game cancellation.</param>
    Task ResolveActionAsync(Services.Framework.GameEngine engine, PlayerModel player, T action, CancellationToken ct);
}