namespace MP.GameEngine.Models.Prompts.PromptTypes.Responses;

/// <summary>
/// Resolution of an <see cref="PromptTypes.AuctionBidPrompt"/>. Carries the
/// bidder's choice — bid an amount or pass for this round.
/// </summary>
public sealed class AuctionBidResponse : PromptResponse
{
    public AuctionBidAction Action { get; init; }

    /// <summary>
    /// The bid value. Required when <see cref="Action"/> is
    /// <see cref="AuctionBidAction.Bid"/>; must be <c>null</c> otherwise.
    /// The validator enforces both that the bid strictly exceeds the
    /// prompt's <see cref="PromptTypes.AuctionBidPrompt.CurrentHighBid"/>
    /// and that it does not exceed
    /// <see cref="PromptTypes.AuctionBidPrompt.PlayerBalance"/>.
    /// </summary>
    public uint? BidAmount { get; init; }
}

/// <summary>The two ways an <see cref="PromptTypes.AuctionBidPrompt"/> can be closed.</summary>
public enum AuctionBidAction
{
    /// <summary>Place a bid; <see cref="AuctionBidResponse.BidAmount"/> must be populated.</summary>
    Bid,

    /// <summary>Decline to bid this round; the bidder drops out of the auction.</summary>
    Pass
}