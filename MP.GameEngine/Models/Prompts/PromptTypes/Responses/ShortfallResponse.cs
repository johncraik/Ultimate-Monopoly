namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of a <see cref="PromptTypes.ShortfallPrompt"/>. Carries the
/// player's chosen way to address the shortfall.
/// </summary>
public sealed class ShortfallResponse : PromptResponse
{
    public ShortfallAction Action { get; init; }
}

/// <summary>
/// The ways a player can respond to a shortfall. The framework only carries
/// the choice; the engine drives the follow-on flow for whichever action is
/// selected.
/// </summary>
public enum ShortfallAction
{
    /// <summary>Take a loan from the bank (auto-sized to cover the shortfall — see Loans rule 2).</summary>
    TakeLoan,

    /// <summary>Raise the balance by mortgaging one or more owned properties.</summary>
    Mortgage,

    /// <summary>Raise the balance by selling buildings back to the bank, one at a time so each sale honours the even-building rule.</summary>
    SellHouses,

    /// <summary>
    /// Open a deal with the creditor whose acceptance settles the debt
    /// directly (see <c>game-rules.md</c> Default rule 7). Only valid when
    /// the shortfall is owed to another player — the validator rejects this
    /// action when the debt is owed to the bank.
    /// </summary>
    ProposeDeal,

    /// <summary>Declare bankruptcy and exit the game (see <c>game-rules.md</c> Bankruptcy).</summary>
    DeclareBankruptcy
}