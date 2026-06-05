using MP.GameEngine.Enums;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

/// <summary>
/// Runs a property auction (<c>game-rules.md</c> Auctions;
/// <c>design-docs/auction-flow.md</c>): a player declines or cannot afford a
/// property they landed on, so it is offered to the table. App-mediated — the
/// engine opens one <see cref="AuctionBidPrompt"/> per bidder at a time and
/// resolves the winner. Money only ever moves through
/// <see cref="TransactionService"/>.
/// </summary>
public class AuctionService
{
    private readonly TransactionService _transactionService;

    public AuctionService(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public record AuctionOutcome(bool Success, PlayerModel? Winner = null, uint Price = 0);
    
    /// <summary>
    /// Auctions the property at <paramref name="boardIndex"/>. The auction opens
    /// at the minimum bid (50% reserve price, grid-rounded); only players who can
    /// afford that floor take part; passing is final; and the last bidder
    /// standing wins at the current bid — the floor if nobody ever raised. No-ops
    /// when nobody can afford the floor, leaving the property bank-owned (very
    /// rare). See <c>auction-flow.md</c>.
    /// </summary>
    public async Task<AuctionOutcome> RunAuction(Framework.GameEngine engine, string playerId, ushort boardIndex, CancellationToken ct)
    {
        var board = engine.Cache.Board;
        var roundingRule = engine.Cache.RoundingRule;

        var floor = MoneyHelper.MinAuctionBid(boardIndex, board, roundingRule);
        var increments = MoneyHelper.AuctionIncrements(roundingRule);
        var propertyName = board.GetBoardSpace(boardIndex).Name;

        //Cite floor rule:
        engine.CiteRule(RuleCode.Auction_MinimumBidHalfPrice);
        
        // Eligible bidders: active players clockwise from (and including) the
        // lander, filtered to those who can afford the floor. A player below the
        // floor can never bid legally, so they take no part — which is also why
        // the eventual winner can always pay (see auction-flow.md §8).
        var bidders = engine.Cache.Game.GetPlayers(playerId, excludePovPlayer: false)
            .Where(p => p.Money >= floor)
            .ToList();

        // Nobody (not even the lander) can afford the minimum — auction cancelled,
        // the landing is a no-op and the property stays with the bank.
        if (bidders.Count == 0)
        {
            engine.CiteRule(RuleCode.Auction_NobodyCanAfford);
            return new AuctionOutcome(false);
        }

        var currentHighBid = floor;
        PlayerModel? highBidder = null;

        var index = 0;
        while (bidders.Count > 1)
        {
            var bidder = bidders[index];

            // Never ask the current high bidder to outbid themselves.
            if (highBidder is not null && bidder.PlayerId == highBidder.PlayerId)
            {
                index = (index + 1) % bidders.Count;
                continue;
            }

            var response = await engine.PromptProvider.RequestAsync(new AuctionBidPrompt
            {
                PlayerId = bidder.PlayerId,
                BoardIndex = boardIndex,
                CurrentHighBid = currentHighBid,
                CurrentHighBidderId = highBidder?.PlayerId,
                PlayerBalance = bidder.Money,
                AllowedIncrements = increments,
                Title = "Property Auction",
                Body = $"{propertyName} is up for auction. The current bid is " +
                       $"{RuleDictionary.Currency}{currentHighBid}. Raise the bid or pass."
            }, ct);

            if (response.Action == AuctionBidAction.Bid)
            {
                // The validator guarantees BidAmount is present, exceeds the
                // current bid, and is within the bidder's balance.
                currentHighBid = response.BidAmount ?? currentHighBid;
                highBidder = bidder;
                index = (index + 1) % bidders.Count;
            }
            else
            {
                // Pass = out: drop the bidder for the rest of the auction. The
                // next player shifts into this slot, so the index stays put
                // (wrapping when the last entry was removed).
                bidders.RemoveAt(index);
                if (index >= bidders.Count) index = 0;
            }
        }

        // One bidder remains — they win at the current bid (the floor if nobody
        // raised; the last player standing wins even without having bid).
        engine.CiteRule(RuleCode.Auction_ForcedLastSurvivor);
        highBidder = bidders[0];
        _ = await engine.PromptProvider.Acknowledge(highBidder.PlayerId, "Auction Won",
            $"You won {propertyName} for {RuleDictionary.Currency}{currentHighBid}.", ct: ct);
        return new AuctionOutcome(true, highBidder, currentHighBid);
    }
}
