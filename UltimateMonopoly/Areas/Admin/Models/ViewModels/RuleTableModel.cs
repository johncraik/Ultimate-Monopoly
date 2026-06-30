using UltimateMonopoly.Models.ViewModels;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels;

public class RuleTableModel
{
    public List<GameRule> Rules { get; }
    public string? Search { get; }

    public RuleTableModel(List<GameRule> rules, string? search)
    {
        Rules = rules;
        Search = search;
    }
}