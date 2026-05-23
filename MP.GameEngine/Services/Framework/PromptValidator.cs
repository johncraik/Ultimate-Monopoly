using MP.GameEngine.Models;
using MP.GameEngine.Models.Prompts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Services.Framework;

/// <summary>
/// Central validator the provider consults before resolving a pending prompt.
/// Returns <c>true</c> if the response is well-formed and the submitter is
/// authorised for this response variant; <c>false</c> otherwise. The provider
/// turns <c>false</c> into a rejected submission without throwing.
/// </summary>
/// <remarks>
/// Each concrete prompt adds its branch to <see cref="Validate"/>. An unknown
/// prompt type is rejected by default — safer than a permissive fallthrough.
/// The host identity is read from <see cref="GameCacheModel.HostPlayerId"/>
/// rather than off the prompt — the host is a property of the game, not of
/// each individual prompt. See <c>design-docs/choice-events.md</c> §6 rule 2.
/// </remarks>
public static class PromptValidator
{
    public static bool Validate(
        Prompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        return prompt switch
        {
            InterruptibleWindowPrompt p => ValidateInterruptibleWindow(p, response, submittingUserId, cache),
            AcknowledgePrompt p => ValidateAcknowledge(p, response, submittingUserId, cache),
            DiceRollPrompt p => ValidateDiceRoll(p, response, submittingUserId, cache),
            AcquirePropertyPrompt p => ValidateAcquireProperty(p, response, submittingUserId, cache),
            _ => false
        };
    }

    /// <summary>
    /// Acquire-property responses are a binary <see cref="AcquirePropertyResponse.Accept"/>
    /// flag — engine decides what "accept" means (buy vs reserve) at the call
    /// site. The validator only checks the submitter is the lander or the host.
    /// </summary>
    private static bool ValidateAcquireProperty(
        AcquirePropertyPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not AcquirePropertyResponse) return false;

        return submittingUserId == prompt.PlayerId
            || submittingUserId == cache.HostPlayerId;
    }

    /// <summary>
    /// Dice roll responses must populate exactly <see cref="DiceRollPrompt.DiceCount"/>
    /// of the three die fields, in order — <see cref="DiceRollResponse.Die1"/>,
    /// <see cref="DiceRollResponse.Die2"/>, <see cref="DiceRollResponse.ThirdDie"/>.
    /// All set values must be in the range 1–6. Submitter must be the named
    /// player or the host.
    /// </summary>
    private static bool ValidateDiceRoll(
        DiceRollPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not DiceRollResponse r) return false;

        if (submittingUserId != prompt.PlayerId && submittingUserId != cache.HostPlayerId)
            return false;

        if (prompt.DiceCount is < 1 or > 3) return false;

        if (!IsValidFace(r.Die1)) return false;

        var die2Required = prompt.DiceCount >= 2;
        if (die2Required != r.Die2.HasValue) return false;
        if (r.Die2 is { } d2 && !IsValidFace(d2)) return false;

        var thirdRequired = prompt.DiceCount == 3;
        if (thirdRequired != r.ThirdDie.HasValue) return false;
        return r.ThirdDie is not { } d3 || IsValidFace(d3);

        static bool IsValidFace(ushort value) => value is >= 1 and <= 6;
    }

    /// <summary>
    /// Acknowledge prompts accept an empty <see cref="AcknowledgeResponse"/> from
    /// the named player, or from the host acting on their behalf via the tablet.
    /// </summary>
    private static bool ValidateAcknowledge(
        AcknowledgePrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not AcknowledgeResponse) return false;

        return submittingUserId == prompt.PlayerId
            || submittingUserId == cache.HostPlayerId;
    }

    /// <summary>
    /// Continue is authority of the host only. PlayCard must name a (player, card)
    /// pair from <see cref="InterruptibleWindowPrompt.EligiblePlays"/>, and the
    /// submitter must be either that player or the host (the host can play any
    /// player's card on their behalf via the tablet — see <c>choice-events.md</c> §9).
    /// </summary>
    private static bool ValidateInterruptibleWindow(
        InterruptibleWindowPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not InterruptibleWindowResponse r) return false;

        return r.Action switch
        {
            InterruptAction.Continue => submittingUserId == cache.HostPlayerId,
            InterruptAction.PlayCard => ValidatePlayCard(prompt, r, submittingUserId, cache),
            _ => false
        };
    }

    private static bool ValidatePlayCard(
        InterruptibleWindowPrompt prompt,
        InterruptibleWindowResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (string.IsNullOrEmpty(response.PlayedByPlayerId)) return false;
        if (string.IsNullOrEmpty(response.PlayedCardId)) return false;

        var named = prompt.EligiblePlays.FirstOrDefault(e =>
            e.PlayerId == response.PlayedByPlayerId && e.CardId == response.PlayedCardId);
        if (named is null) return false;

        return submittingUserId == response.PlayedByPlayerId
            || submittingUserId == cache.HostPlayerId;
    }
}
