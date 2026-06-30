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
            LeaveJailPrompt p => ValidateLeaveJail(p, response, submittingUserId, cache),
            AcquirePropertyPrompt p => ValidateAcquireProperty(p, response, submittingUserId, cache),
            TargetPlayerPrompt p => ValidateTargetPlayer(p, response, submittingUserId, cache),
            TargetPropertyPrompt p => ValidateTargetProperty(p, response, submittingUserId, cache),
            ShortfallPrompt p => ValidateShortfall(p, response, submittingUserId, cache),
            AuctionBidPrompt p => ValidateAuctionBid(p, response, submittingUserId, cache),
            CardOptionPrompt p => ValidateCardOption(p, response, submittingUserId, cache),
            BuildDealPrompt p => ValidateBuildDeal(p, response, submittingUserId, cache),
            DealPrompt p => ValidateDeal(p, response, submittingUserId, cache),
            _ => false
        };
    }

    /// <summary>
    /// A built deal must come from the named proposer (the debtor) or the host.
    /// Every property the proposer puts up must be drawn from the prompt's
    /// <see cref="BuildDealPrompt.ProposerDealableIndexes"/> and every property
    /// the counter party puts up from
    /// <see cref="BuildDealPrompt.CounterPartyDealableIndexes"/>, with no
    /// duplicates on either side; and each side's offered money must not exceed
    /// that side's cash (<see cref="BuildDealPrompt.ProposerBalance"/> /
    /// <see cref="BuildDealPrompt.CounterPartyBalance"/>) — deal spend comes from
    /// cash on hand (<c>game-rules.md</c> Default rule 7).
    /// </summary>
    private static bool ValidateBuildDeal(
        BuildDealPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not BuildDealResponse r) return false;

        if (submittingUserId != prompt.PlayerId && submittingUserId != cache.HostPlayerId)
            return false;

        var contents = r.Contents;
        if (contents is null) return false;

        if (contents.MoneyFromProposer > prompt.ProposerBalance) return false;
        if (contents.MoneyFromCounterParty > prompt.CounterPartyBalance) return false;

        return AllDrawnFrom(contents.PropertiesFromProposer, prompt.ProposerDealableIndexes)
            && AllDrawnFrom(contents.PropertiesFromCounterParty, prompt.CounterPartyDealableIndexes);

        static bool AllDrawnFrom(IReadOnlyList<ushort> selected, IReadOnlyList<ushort> eligible)
        {
            if (selected.Distinct().Count() != selected.Count) return false;   // no duplicates
            var set = eligible.ToHashSet();
            return selected.All(set.Contains);
        }
    }

    /// <summary>
    /// A deal accept/decline must come from the named counter party or the host.
    /// The response is a bare <see cref="Responses.DealResponse.Accept"/> bool —
    /// the contents are server-authored on the prompt and not re-supplied, so
    /// there is nothing further to validate.
    /// </summary>
    private static bool ValidateDeal(
        DealPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not DealResponse) return false;

        return submittingUserId == prompt.PlayerId
            || submittingUserId == cache.HostPlayerId;
    }

    /// <summary>
    /// Auction bids must come from the named bidder (or the host on their
    /// behalf). A <see cref="AuctionBidAction.Bid"/> response must carry a
    /// <see cref="AuctionBidResponse.BidAmount"/> that strictly exceeds the
    /// prompt's <see cref="AuctionBidPrompt.CurrentHighBid"/> and does not
    /// exceed <see cref="AuctionBidPrompt.PlayerBalance"/> — bidders may
    /// not exceed the cash they genuinely have (<c>game-rules.md</c>
    /// Default rule 7). A <see cref="AuctionBidAction.Pass"/> response
    /// must have a <c>null</c> bid amount.
    /// </summary>
    private static bool ValidateAuctionBid(
        AuctionBidPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not AuctionBidResponse r) return false;

        if (submittingUserId != prompt.PlayerId && submittingUserId != cache.HostPlayerId)
            return false;

        return r.Action switch
        {
            AuctionBidAction.Bid => r.BidAmount is { } amount
                                    && amount > prompt.CurrentHighBid
                                    && amount <= prompt.PlayerBalance,
            AuctionBidAction.Pass => r.BidAmount is null,
            _ => false
        };
    }

    /// <summary>
    /// Card-option responses must come from the named chooser (or the host),
    /// and the <see cref="CardOptionResponse.SelectedKey"/> must match one
    /// of the prompt's <see cref="CardOptionPrompt.Options"/> by key.
    /// </summary>
    private static bool ValidateCardOption(
        CardOptionPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not CardOptionResponse r) return false;

        if (submittingUserId != prompt.PlayerId && submittingUserId != cache.HostPlayerId)
            return false;

        //A play-card choice may decline (empty key) OR pick one of the offered cards — but NOT an
        //arbitrary key (M-01: the old `PlayCardChoice || …` accepted ANY key, letting a bogus response
        //drive a card play). A mandatory choice (group pick / card steal) must name a real option.
        return prompt.PlayCardChoice
            ? string.IsNullOrEmpty(r.SelectedKey) || prompt.Options.Any(o => o.Key == r.SelectedKey)
            : prompt.Options.Any(o => o.Key == r.SelectedKey);
    }

    /// <summary>
    /// Target-player selection must come from the named chooser (or the host
    /// acting on their behalf), pick exactly <see cref="TargetPlayerPrompt.Count"/>
    /// players, draw every selection from <see cref="TargetPlayerPrompt.EligiblePlayerIds"/>,
    /// and contain no duplicates.
    /// </summary>
    private static bool ValidateTargetPlayer(
        TargetPlayerPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not TargetPlayerResponse r) return false;

        if (submittingUserId != prompt.PlayerId && submittingUserId != cache.HostPlayerId)
            return false;

        // Required count is clamped to the eligible set so a caller asking for more targets
        // than exist cannot make the prompt unsatisfiable (R-01). Uses Math.Min rather than
        // "== Count || == eligible.Count" — the latter would also accept selecting *all*
        // options when the caller only wanted a subset.
        var required = Math.Min((int)prompt.Count, prompt.EligiblePlayerIds.Count);
        if (r.SelectedPlayerIds.Count != required) return false;
        if (r.SelectedPlayerIds.Distinct().Count() != r.SelectedPlayerIds.Count) return false;

        var eligible = prompt.EligiblePlayerIds.ToHashSet();
        return r.SelectedPlayerIds.All(eligible.Contains);
    }

    /// <summary>
    /// Target-property selection must come from the named chooser (or the
    /// host acting on their behalf), pick exactly
    /// <see cref="TargetPropertyPrompt.Count"/> properties, draw every
    /// selection from <see cref="TargetPropertyPrompt.EligibleBoardIndexes"/>,
    /// and contain no duplicates.
    /// </summary>
    private static bool ValidateTargetProperty(
        TargetPropertyPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not TargetPropertyResponse r) return false;

        if (submittingUserId != prompt.PlayerId && submittingUserId != cache.HostPlayerId)
            return false;

        // Required count is clamped to the eligible set so a caller asking for more properties
        // than exist cannot make the prompt unsatisfiable (R-01). Uses Math.Min rather than
        // "== Count || == eligible.Count" — the latter would also accept selecting *all*
        // options when the caller only wanted a subset.
        var required = Math.Min((int)prompt.Count, prompt.EligibleBoardIndexes.Count);
        if (r.SelectedBoardIndexes.Count != required) return false;
        if (r.SelectedBoardIndexes.Distinct().Count() != r.SelectedBoardIndexes.Count) return false;

        var eligible = prompt.EligibleBoardIndexes.ToHashSet();
        return r.SelectedBoardIndexes.All(eligible.Contains);
    }

    /// <summary>
    /// Shortfall responses carry a single <see cref="ShortfallAction"/> from
    /// the player who owes the debt (or the host on their behalf).
    /// <see cref="ShortfallAction.ProposeDeal"/> is rejected when the debt is
    /// owed to the bank — there is no creditor to settle with. Other actions
    /// are accepted at the framework level; the engine handles whether the
    /// chosen path is actually achievable (sufficient buildings to sell,
    /// loan slots available, etc.).
    /// </summary>
    private static bool ValidateShortfall(
        ShortfallPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not ShortfallResponse r) return false;

        if (submittingUserId != prompt.PlayerId && submittingUserId != cache.HostPlayerId)
            return false;

        return r.Action switch
        {
            ShortfallAction.TakeLoan => true,
            ShortfallAction.Mortgage => true,
            ShortfallAction.SellHouses => true,
            ShortfallAction.ProposeDeal => prompt.OwedToPlayerId is not null,
            ShortfallAction.DeclareBankruptcy => true,
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
    /// Leave-jail responses come from the jailed player (or the host on their
    /// behalf). <see cref="LeaveJailAction.PayFee"/> is always valid;
    /// <see cref="LeaveJailAction.PlayCard"/> is rejected unless the prompt
    /// reports the player holds a Get Out of Jail Free card.
    /// </summary>
    private static bool ValidateLeaveJail(
        LeaveJailPrompt prompt,
        PromptResponse response,
        string submittingUserId,
        GameCacheModel cache)
    {
        if (response is not LeaveJailResponse r) return false;

        if (submittingUserId != prompt.PlayerId && submittingUserId != cache.HostPlayerId)
            return false;

        return r.Action switch
        {
            LeaveJailAction.PayFee => true,
            LeaveJailAction.PlayCard => prompt.HasCard,
            _ => false
        };
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
