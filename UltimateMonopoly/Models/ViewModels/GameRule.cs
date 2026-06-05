using MP.GameEngine.Enums;

namespace UltimateMonopoly.Models.ViewModels;

public class GameRule
{
    public const string RawCode = "<NONE>";
    
    public int Section { get; set; }
    public int Rule { get; set; }
    public char? Point { get; set; }
    
    public string Title { get; set; }
    public string RuleDescription { get; set; }
    public string RawRuleCode { get; set; }

    public string? RuleIdLink => RuleCode == null 
        ? null 
        : RuleCode.ToString();
    
    public RuleCode? RuleCode => string.Equals(RawRuleCode, RawCode, StringComparison.OrdinalIgnoreCase)
        ? null 
        : Enum.TryParse(RawRuleCode, out RuleCode ruleCode) 
            ? ruleCode : null;
}