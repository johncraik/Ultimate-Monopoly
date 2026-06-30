using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Prompts;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

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
    /// Sends an acknowledgment prompt to a specific player, allowing the game engine
    /// to display a message that requires no further action from the player but serves
    /// as an informational or confirmation-based interaction.
    /// </summary>
    /// <param name="playerId">
    /// The unique identifier of the player to whom the acknowledgment prompt is directed.
    /// </param>
    /// <param name="title">
    /// The title of the acknowledgment message that will be displayed to the player.
    /// </param>
    /// <param name="body">
    /// The main content of the acknowledgment message that provides detailed information
    /// or context for the interaction.
    /// </param>
    /// <param name="timeout">
    /// The optional time duration after which the acknowledgment prompt will expire
    /// if the player does not acknowledge it.
    /// </param>
    /// <param name="cardType">
    /// The deck a drawn card came from when this acknowledge announces a card pick-up — drives
    /// card-type-flavoured styling on the front end. <c>null</c> (the default) for every other
    /// acknowledge, which renders in the default secondary styling.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that allows the operation to be canceled before completion,
    /// typically used for handling game-level cancellation scenarios.
    /// </param>
    /// <returns>
    /// An instance of <see cref="AcknowledgeResponse"/> that encapsulates the result of
    /// the player's acknowledgment interaction.
    /// </returns>
    Task<AcknowledgeResponse> Acknowledge(
        string playerId,
        string title,
        string body,
        TimeSpan? timeout = null,
        CardType? cardType = null,
        bool playingCard = false,
        CancellationToken ct = default);

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
