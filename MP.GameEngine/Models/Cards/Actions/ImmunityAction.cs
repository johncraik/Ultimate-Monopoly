using MP.GameEngine.Enums.Cards;

namespace MP.GameEngine.Models.Cards.Actions;

public sealed class ImmunityAction : CardAction
{
    public CardImmunity Immunity { get; set; }
}