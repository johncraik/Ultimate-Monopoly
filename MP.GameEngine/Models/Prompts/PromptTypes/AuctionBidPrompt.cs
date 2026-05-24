using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts.PromptTypes;

/// <summary>
/// Asks <see cref="Prompt.PlayerId"/> to bid on the property being auctioned,
/// or to pass. The engine runs the auction loop and opens one of these
/// prompts per bidder per round, in clockwise order
/// (<c>game-rules.md</c> Default rule 6 — every player may bid, including
/// the player who declined the purchase and any players currently in jail).
/// </summary>
/// <remarks>
/// Per <c>game-rules.md</c> Default rule 7, bidding must be paid from money
/// the player genuinely has — they cannot mortgage, sell buildings, or
/// trade to fund a bid. The validator enforces this by capping
/// <see cref="AuctionBidResponse.BidAmount"/> at <see cref="PlayerBalance"/>.
/// </remarks>
public sealed class AuctionBidPrompt : Prompt<AuctionBidResponse>
{
    /// <summary>
    /// The property being auctioned, by
    /// <see cref="Snapshot.PropertyModel.BoardIndex"/>.
    /// </summary>
    public ushort BoardIndex { get; init; }

    /// <summary>
    /// The highest bid so far. A new bid must strictly exceed this — the
    /// minimum legal bid is <c>CurrentHighBid + 1</c>. <c>0</c> at the
    /// start of the auction before any bids have been placed.
    /// </summary>
    public uint CurrentHighBid { get; init; }

    /// <summary>
    /// The player who currently holds the high bid, if any. <c>null</c>
    /// before the first bid lands. Useful for the frontend ("Player X is
    /// winning at £350") but not consulted by validation.
    /// </summary>
    public string? CurrentHighBidderId { get; init; }

    /// <summary>The bidder's available cash. Bids above this are rejected by the validator.</summary>
    public uint PlayerBalance { get; init; }

    public override PromptTarget Target => PromptTarget.SinglePlayer(PlayerId);
}