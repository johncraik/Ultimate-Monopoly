using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Helpers;

public static class MoneyHelper
{
    /// <summary>
    /// Normalises the provided monetary amount based on the specified rounding rule and financial context.
    /// </summary>
    /// <param name="amount">
    /// The monetary amount to be rounded. This value may be positive or negative.
    /// </param>
    /// <param name="roundingRule">
    /// The rounding rule to apply to the monetary amount. This determines how the amount is adjusted.
    /// </param>
    /// <param name="reason">
    /// The financial context or reason for the rounding. This can impact how special cases are handled.
    /// </param>
    /// <returns>
    /// The normalized monetary amount after applying the rounding rule and adjustments based on the financial context.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an unsupported or invalid rounding rule is provided in the <paramref name="roundingRule"/> parameter.
    /// </exception>
    public static long NormaliseAmount(long amount, GameRoundingRule roundingRule, FinancialReason reason)
    {
        var value = roundingRule switch
        {
            GameRoundingRule.None => amount,
            GameRoundingRule.To5 => (long)(Math.Round(amount / 5.0, MidpointRounding.AwayFromZero) * 5),
            GameRoundingRule.To10 => (long)(Math.Round(amount / 10.0, MidpointRounding.AwayFromZero) * 10),
            GameRoundingRule.To20 => (long)(Math.Round(amount / 20.0, MidpointRounding.AwayFromZero) * 20),
            GameRoundingRule.To50 => (long)(Math.Round(amount / 50.0, MidpointRounding.AwayFromZero) * 50),
            _ => throw new ArgumentOutOfRangeException(nameof(roundingRule), roundingRule, null)
        };

        switch (reason)
        {
            case FinancialReason.Rent:
            case FinancialReason.TripleBonus:
            case FinancialReason.GoBonus:
            case FinancialReason.TurnTax:
                //Rent that resolves to 0, is 0. All others round UP to minimum value
                //Triple or GO bonus that is 0, stays 0 (receive no bonus cards)
                //Turn tax that is 0, stays 0 (no tax)
                return value;
            case FinancialReason.LoanTake:
                //Loan take is always rounded UP
                value = value < amount
                    ? value + roundingRule switch
                    {
                        GameRoundingRule.None => 0,
                        GameRoundingRule.To5 => 5,
                        GameRoundingRule.To10 => 10,
                        GameRoundingRule.To20 => 20,
                        GameRoundingRule.To50 => 50,
                        _ => throw new ArgumentOutOfRangeException(nameof(roundingRule), roundingRule, null)
                    }
                    : value;
                break;
        }

        var positive = amount > 0;
        if (value == 0)
            value = roundingRule switch
            {
                GameRoundingRule.None => value,
                GameRoundingRule.To5 => positive ? 5 : -5,
                GameRoundingRule.To10 => positive ? 10 : -10,
                GameRoundingRule.To20 => positive ? 20 : -20,
                GameRoundingRule.To50 => positive ? 50 : -50,
                _ => throw new ArgumentOutOfRangeException(nameof(roundingRule), roundingRule, null)
            };

        return value;
    }

    /// <summary>
    /// Normalises the given monetary amount based on the specified game rounding rule and financial reason.
    /// </summary>
    /// <param name="amount">
    /// The monetary amount to be normalised. This value is represented as an unsigned integer and must be non-negative.
    /// </param>
    /// <param name="roundingRule">
    /// The game rounding rule to be applied to the amount. Determines how the value is adjusted (e.g., to the nearest 5, 10, etc.).
    /// </param>
    /// <param name="reason">
    /// The financial reason related to the operation. Provides contextual information about the monetary adjustment.
    /// </param>
    /// <returns>
    /// A normalised monetary amount as an unsigned integer after applying the specified rounding rule.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="roundingRule"/> is not a supported value.
    /// </exception>
    public static uint NormaliseAmount(uint amount, GameRoundingRule roundingRule, FinancialReason reason)
        => (uint)NormaliseAmount((long)amount, roundingRule, reason);


    /// <summary>
    /// Normalises the provided monetary amount to a positive value after applying the specified rounding rule and financial context.
    /// </summary>
    /// <param name="amount">
    /// The monetary amount to be processed. This value can be positive or negative.
    /// </param>
    /// <param name="roundingRule">
    /// The rounding rule to apply to the monetary amount. It dictates how rounding adjustments are handled.
    /// </param>
    /// <param name="reason">
    /// The financial context or reason for the rounding and adjustment. This defines the purpose of normalization.
    /// </param>
    /// <returns>
    /// The normalized and positive monetary amount, converted to an unsigned integer, after applying the rounding rule and specified adjustments.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an invalid or unsupported rounding rule is provided in the <paramref name="roundingRule"/> parameter.
    /// </exception>
    public static uint NormaliseAmountToPositive(long amount, GameRoundingRule roundingRule, FinancialReason reason)
        => (uint)Math.Abs(NormaliseAmount(amount, roundingRule, reason));


    private static uint HalfPrice(uint amount)
        => (uint)Math.Round((amount / 2d), MidpointRounding.AwayFromZero);

    public static uint ReservePrice(ushort index, Board board, GameRoundingRule roundingRule)
    {
        var space = board.GetBoardSpace(index);
        if(!space.IsPurchasable || space.PurchaseCost == null)
            return 0;
        
        var price = HalfPrice((uint)space.PurchaseCost);
        return NormaliseAmount(price, roundingRule, FinancialReason.Purchase);
    }
    
    public static uint UnMortgageCost(ushort index, Board board, GameRoundingRule roundingRule)
    {
        var space = board.GetBoardSpace(index);
        if(!space.IsPurchasable || space.PurchaseCost == null)
            return 0;
        
        var price = HalfPrice((uint)space.PurchaseCost);
        //Unmortgage fee is 10% of the purchase price
        var unmortgageFee = Math.Round((price * 0.1), MidpointRounding.AwayFromZero);
        
        price += (uint)unmortgageFee;
        return NormaliseAmount(price, roundingRule, FinancialReason.Purchase);
        
    }
    
    public static uint MortgageValue(ushort index, Board board, GameRoundingRule roundingRule)
        //Mortgage value is half the purchase price, same as reserve price
        //NOTE: checks if purchasable, but all mortgageable properties are purchasable
        => ReservePrice(index, board, roundingRule);

    /// <summary>
    /// The GO repayment fee for a single mortgaged property — a percentage of its
    /// <b>purchase cost</b> (<c>game-rules.md</c> Mortgaging rule 1), grid-rounded
    /// per property (never to zero). Shared by the engine charge
    /// (<c>PropertyService.PayMortgageFee</c>) and the profile display so the two
    /// always agree; sum it across the player's mortgaged properties for the total.
    /// </summary>
    public static uint MortgageFee(ushort index, Board board, GameRoundingRule roundingRule)
    {
        var space = board.GetBoardSpace(index);
        if (!space.IsPurchasable || space.PurchaseCost == null)
            return 0;

        var fee = (uint)Math.Round(((uint)space.PurchaseCost * RuleDictionary.MortgageFee), MidpointRounding.AwayFromZero);
        return NormaliseAmount(fee, roundingRule, FinancialReason.MortgageFee);
    }
    
    public static uint MinAuctionBid(ushort index, Board board, GameRoundingRule roundingRule)
        //Minimum bid price is half the purchase price, same as reserve price
        => ReservePrice(index, board, roundingRule);
    
    

    public static ushort[] AuctionIncrements(GameRoundingRule roundingRule)
    {
        var increments = new List<ushort> { 50, 100 };
        switch (roundingRule)
        {
            case GameRoundingRule.None:
                increments.AddRange([1, 5, 10, 20]);
                break;
            case GameRoundingRule.To5:
                increments.AddRange([5, 10, 20]);
                break;
            case GameRoundingRule.To10:
                increments.AddRange([10, 20]);
                break;
            case GameRoundingRule.To20:
                increments.Add(20);
                break;
            case GameRoundingRule.To50:
            default:
                break;
        }
        
        return increments.Order().ToArray();
    }
}