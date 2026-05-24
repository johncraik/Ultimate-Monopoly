using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Opens when the engine has computed a payment <see cref="Prompt.PlayerId"/> cannot
/// meet from their cash on hand. The response is a single
/// <see cref="ShortfallAction"/> — the player's chosen path for raising
/// the balance (loan, mortgage, sell buildings, propose a settling deal) or
/// surrender (declare bankruptcy). See <c>game-rules.md</c> Default rule 7,
/// Loans, Mortgaging, and Bankruptcy.
/// </summary>
/// <remarks>
/// Engine flow: the chosen action is handled by a subsequent prompt or
/// command (e.g. <see cref="TargetPropertyPrompt"/> to pick what to mortgage
/// or sell). If after that step the player is still short, the engine
/// re-opens this prompt. The framework only carries the choice — the engine
/// drives the loop.
/// </remarks>
public sealed class ShortfallPrompt : Prompt<ShortfallResponse>
{
    /// <summary>The total amount the player has to pay.</summary>
    public uint Cost { get; init; }

    /// <summary>The player's available cash at the moment the shortfall is computed.</summary>
    public uint PlayerBalance { get; init; }

    /// <summary>
    /// How much the player still needs to find — <see cref="Cost"/> minus
    /// <see cref="PlayerBalance"/>. Computed from the wire fields so the
    /// engine and the frontend always agree on a single derivation. The
    /// getter is serialised on the way out (the frontend gets the value
    /// pre-computed) and skipped on the way in (no setter — tampered
    /// values can't poison the prompt; the server re-derives it).
    /// </summary>
    public uint AmountOwed => Cost - PlayerBalance;

    /// <summary>
    /// The creditor, if the debt is owed to another player (rent, fine paid
    /// directly to a player, etc.). <c>null</c> when the debt is owed to the
    /// bank — in that case <see cref="ShortfallAction.ProposeDeal"/> is not a
    /// valid response (there is no creditor to settle with), and the
    /// validator rejects it.
    /// </summary>
    public string? OwedToPlayerId { get; init; }

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}