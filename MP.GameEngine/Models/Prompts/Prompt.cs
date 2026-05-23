using System.Text.Json.Serialization;
using MP.GameEngine.Models.Prompts.PromptTypes;

namespace MP.GameEngine.Models.Prompts;

/// <summary>
/// Base class for anything the engine emits that pauses execution until a
/// response comes back. See <c>design-docs/choice-events.md</c>.
/// </summary>
/// <remarks>
/// Concrete prompts derive from <see cref="Prompt{TResponse}"/>, which pairs
/// the prompt with the typed response shape the engine awaits.
/// </remarks>
[JsonPolymorphic]
[JsonDerivedType(typeof(InterruptibleWindowPrompt), "InterruptibleWindow")]
[JsonDerivedType(typeof(AcknowledgePrompt), "Acknowledge")]
[JsonDerivedType(typeof(DiceRollPrompt), "DiceRoll")]
[JsonDerivedType(typeof(AcquirePropertyPrompt), "AcquireProperty")]
public abstract class Prompt
{
    /// <summary>
    /// Unique identifier for this prompt instance. The response must echo this
    /// value; the provider rejects mismatches.
    /// </summary>
    public string PromptId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The audience that will see this prompt. Authorisation for individual
    /// response variants is enforced separately in the validator.
    /// </summary>
    public abstract PromptTarget Target { get; }

    /// <summary>
    /// Advisory only — the framework does not run timers. The web layer may
    /// schedule a client-side timeout and submit <see cref="DefaultResponse"/>
    /// when it elapses.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Response submitted on behalf of the target if a client-side timeout
    /// elapses. <c>null</c> means the prompt has no default and never
    /// auto-resolves.
    /// </summary>
    public PromptResponse? DefaultResponse { get; init; }

    /// <summary>Short heading shown to the player (e.g. "Your turn", "Cannot afford").</summary>
    public string Title { get; init; } = "";

    /// <summary>Longer prose describing the situation and the choice (e.g. "Buy Pall Mall for £70 — completes your pink set").</summary>
    public string Body { get; init; } = "";
}

/// <summary>
/// A prompt paired with its typed response. The engine awaits
/// <see cref="Abstractions.IPromptProvider.RequestAsync{TResponse}"/> on a
/// concrete <see cref="Prompt{TResponse}"/> and receives the matching
/// response.
/// </summary>
public abstract class Prompt<TResponse> : Prompt
    where TResponse : PromptResponse
{
}
