using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;

namespace MP.GameEngine.Helpers.Cards;

public static class CardDisplayHelper
{
    public const string GroupIdentifier = "G";
    private const string GroupKeySeparator = "__";
    private const char CostTagOpen = '{';
    private const char CostTagClose = '}';

    /// <summary>
    /// Replaces a specific cost tag in the card text with the specified cost, formatted with the game's currency symbol.
    /// </summary>
    /// <param name="cardText">The text of the card containing the cost tag.</param>
    /// <param name="groupKey">The key identifying the group to which the cost tag belongs.</param>
    /// <param name="costIndex">The index of the cost tag to be replaced within the group.</param>
    /// <param name="cost">The cost value to replace the tag with.</param>
    /// <returns>The card text with the specified cost tag replaced by the formatted cost value.</returns>
    private static string ReplaceCostTag(this string cardText, string groupKey, int costIndex, uint cost)
        => cardText.Replace($"{CostTagOpen}{groupKey}{GroupKeySeparator}{costIndex}{CostTagClose}",
            $"{RuleDictionary.Currency}{cost:n}");

    /// <summary>
    /// Replaces a cost tag in the group text with the specified cost, formatted
    /// with the game's currency symbol.
    /// </summary>
    /// <param name="cardText">The text of the card containing the cost tag.</param>
    /// <param name="costIndex">The index of the cost tag to be replaced.</param>
    /// <param name="cost">The cost value to replace the tag with.</param>
    /// <returns>A string with the cost tag replaced by the cost value formatted with the currency symbol.</returns>
    private static string ReplaceCostTag(this string cardText, int costIndex, uint cost)
        => cardText.Replace($"{CostTagOpen}{costIndex}{CostTagClose}",
            $"{RuleDictionary.Currency}{cost:n}");


    public static string FormatCardText(this string text, CardGroup g, ushort playerCap, 
        GameRoundingRule roundingRule, bool isGroupText)
    {
        for (var i = 0; i < g.Actions.Count; i++)
        {
            var a = g.Actions[i];
            if(a is not MoneyAction moneyAction) continue;

            var cost = moneyAction.Amount;
            
            //ONLY apply % cap and rounding if value is NOT multiplied (cap and rounding applies AFTER multiplying amount)
            var displayCost = (uint)Math.Abs(cost);
            if(moneyAction is { PerUnit: MoneyPerUnit.None, DiceMultiplier: DiceMultiplier.None })
            {
                if(moneyAction.PercentageApplies)
                    cost = (cost * playerCap) / 100;
                
                displayCost = MoneyHelper.NormaliseAmountToPositive(cost, roundingRule, 
                    cost < 0 ? FinancialReason.CardCharge : FinancialReason.CardPayout);
            }
                
            text = isGroupText 
                ? text.ReplaceCostTag(i, displayCost) 
                : text.ReplaceCostTag(g.GroupKey, i, displayCost);
        }

        return text;
    }
}