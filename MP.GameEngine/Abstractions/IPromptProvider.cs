using MP.GameEngine.Models.Prompts;

namespace MP.GameEngine.Abstractions;

/// <summary>
/// The seam between the engine and the outside world for pause-and-await
/// interactions. The engine awaits <see cref="RequestAsync{TResponse}"/>; the
/// web/SignalR layer calls <see cref="TrySubmit"/> when a client responds.
/// See <c>design-docs/choice-events.md</c>.
/// </summary>
public interface IPromptProvider
{
    /// <summary>
    /// Opens a prompt and suspends the calling engine code until a valid
    /// response is submitted. Throws <see cref="OperationCanceledException"/>
    /// if <paramref name="ct"/> is cancelled before a response arrives.
    /// </summary>
    /// <typeparam name="TResponse">The response type paired with the prompt.</typeparam>
    /// <param name="prompt">The prompt to open. Becomes the cache's pending prompt.</param>
    /// <param name="ct">Cancellation token — typically the game's cancellation token.</param>
    Task<TResponse> RequestAsync<TResponse>(
        Prompt<TResponse> prompt,
        CancellationToken ct = default)
        where TResponse : PromptResponse;

    /// <summary>
    /// Attempts to resolve the open prompt with the given response. Returns
    /// <c>false</c> (never throws) on any failure: stale concurrency stamp,
    /// no prompt open, mismatched prompt id, validation rejection, or the
    /// submitter not being authorised for this response variant.
    /// </summary>
    /// <param name="submittingUserId">The identity of the user submitting — used by validation to enforce per-variant authorisation.</param>
    /// <param name="concurrencyStamp">The cache's concurrency stamp at the time the client read it.</param>
    /// <param name="response">The submitted response.</param>
    bool TrySubmit(string submittingUserId, string concurrencyStamp, PromptResponse response);
}
