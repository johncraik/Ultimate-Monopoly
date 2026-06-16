using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Conditions;

namespace MP.GameEngine.Models.Imports;

public class CardJsonImport
{
    public string RawText { get; set; } = "";
    public string CardType { get; set; } = "";

    public List<CardGroup> Groups { get; set; } = [];
    public List<CardCondition> Conditions { get; set; } = [];
    public string ConditionType { get; set; } = "";

    public SuppressDefault SuppressDefault { get; set; } = new(SuppressDefaultType.None);
}