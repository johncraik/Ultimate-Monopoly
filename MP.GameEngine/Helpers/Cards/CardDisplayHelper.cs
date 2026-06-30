using System.Text.RegularExpressions;
using JC.Core.Extensions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Helpers.Cards;

public static class CardDisplayHelper
{
    private const string OccasionsIdentifier = "OC";
    public const string GroupIdentifier = "G";
    private const string KeySeparator = "__";
    private const char TagOpen = '{';
    private const char TagClose = '}';
    
    public const string UniqueTagOpen = "[[";
    public const string UniqueTagClose = "]]";

    /// <summary>
    /// Replaces a specific cost tag in the card text with the specified cost, formatted with the game's currency symbol.
    /// </summary>
    /// <param name="cardText">The text of the card containing the cost tag.</param>
    /// <param name="groupKey">The key identifying the group to which the cost tag belongs.</param>
    /// <param name="costIndex">The index of the cost tag to be replaced within the group.</param>
    /// <param name="cost">The cost value to replace the tag with.</param>
    /// <returns>The card text with the specified cost tag replaced by the formatted cost value.</returns>
    private static string ReplaceCostTag(this string cardText, string groupKey, int costIndex, uint cost)
        => cardText.Replace($"{TagOpen}{groupKey}{KeySeparator}{costIndex}{TagClose}",
            $"{RuleDictionary.Currency}{cost:N0}");

    /// <summary>
    /// Replaces a cost tag in the group text with the specified cost, formatted
    /// with the game's currency symbol.
    /// </summary>
    /// <param name="cardText">The text of the card containing the cost tag.</param>
    /// <param name="costIndex">The index of the cost tag to be replaced.</param>
    /// <param name="cost">The cost value to replace the tag with.</param>
    /// <returns>A string with the cost tag replaced by the cost value formatted with the currency symbol.</returns>
    private static string ReplaceCostTag(this string cardText, int costIndex, uint cost)
        => cardText.Replace($"{TagOpen}{costIndex}{TagClose}",
            $"{RuleDictionary.Currency}{cost:N0}");
    
    private static string ReplaceOccasionsTag(this string cardText, int occasionsIndex, int occasionsCount)
        => cardText.Replace($"{TagOpen}{OccasionsIdentifier}{KeySeparator}{occasionsIndex}{TagClose}",
            $"{occasionsCount:N0}");

    public static string FormatCardText(this string text, CardGroup g, ushort playerCap, 
        GameRoundingRule roundingRule, bool isGroupText)
    {
        for (var i = 0; i < g.Actions.Count; i++)
        {
            var a = g.Actions[i];
            if (a is MoneyAction moneyAction)
            {
                var cost = moneyAction.Amount;
            
                //ONLY apply % cap and rounding if value is NOT multiplied (cap and rounding applies AFTER multiplying amount)
                var displayCost = (uint)Math.Abs(cost);
                if(moneyAction is { PerUnit: MoneyPerUnit.None, DiceMultiplier: DiceMultiplier.None })
                {
                    if(moneyAction.PercentageApplies)
                        cost = (cost * playerCap) / 100;
                
                    displayCost = MoneyHelper.NormaliseAmountToPositive((long)Math.Round(cost, MidpointRounding.AwayFromZero), roundingRule,
                        cost < 0 ? FinancialReason.CardCharge : FinancialReason.CardPayout);
                }
                
                text = isGroupText 
                    ? text.ReplaceCostTag(i, displayCost) 
                    : text.ReplaceCostTag(g.GroupKey, i, displayCost);
            }
            
            if(g.TurnsRemaining == null) continue;
            
            text = text.ReplaceOccasionsTag(i, (int)g.TurnsRemaining);
        }

        return text;
    }

    public static string CardColourDisplay(CardType type, bool playing)
        => "card-" + type switch
        {
            CardType.Chance => $"{PlayingSlug(playing)}chance",
            CardType.ComChest => $"{PlayingSlug(playing)}com-chest",
            CardType.PercentageChance => $"{PlayingSlug(playing)}percent-chance",
            CardType.PercentageComChest => $"{PlayingSlug(playing)}percent-com-chest",
            CardType.Third => $"{PlayingSlug(playing)}third",
            CardType.Double => $"{PlayingSlug(playing)}double",
            CardType.Triple => $"{PlayingSlug(playing)}triple",
            CardType.Tax => $"{PlayingSlug(playing)}tax",
            CardType.Go => $"{PlayingSlug(playing)}go",
            CardType.JustVisiting => $"{PlayingSlug(playing)}jv",
            CardType.FreeParking => $"{PlayingSlug(playing)}fp",
            CardType.GoToJail => $"{PlayingSlug(playing)}jail",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    
    private static string PlayingSlug(bool playing)
        => playing ? "play-" : "";


    public static string ConditionDisplay(this CardModel card)
        => card.ConditionType switch
        {
            CardConditionType.None => "No Conditions", //Should never be displayed since all kept cards have a condition
            CardConditionType.MetCardholderTurn or CardConditionType.MetAnyPlayerTurn => "Forced",
            CardConditionType.ChoiceCardholderTurn or CardConditionType.ChoiceAnyPlayerTurn => "Choice",
            _ => throw new ArgumentOutOfRangeException()
        };

    public static string CardTriggerDisplay(this CardModel card)
    {
        if(card.ConditionType == CardConditionType.None 
           || card.Conditions.All(c => c.Trigger == CardTrigger.None))
            return "None";
        
        var triggers = card.Conditions.Select(c => c.Trigger).ToList();
        return string.Join(" or ", triggers.Select(t => t.ToDisplayName()));
    }
}