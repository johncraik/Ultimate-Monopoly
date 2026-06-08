namespace UltimateMonopoly.Models.ViewModels.Games;

/// <summary>
/// Render inputs for the <c>_MakeDealPartial</c> deal builder. Carries only the
/// game id and the two participants; the partial resolves each side's dealable
/// property pool, balances, and display names from the game cache itself. Shared
/// by both deal entry points — the turn-boundary Deal tab (command) and the
/// <c>BuildDealPrompt</c> (shortfall) — so it stays decoupled from either.
/// See <c>design-docs/game-deals.md</c> §14.
/// </summary>
/// <param name="GameId">The game whose cache the builder reads (the caller supplies it).</param>
/// <param name="ProposerId">The player on the "What you give" side (the deal's proposer).</param>
/// <param name="CounterPartyId">The player on the "What you receive" side.</param>
public sealed record MakeDealViewModel(string GameId, string ProposerId, string CounterPartyId);