using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Cards.Conditions;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Services.Cards;

/// <summary>
/// Temporary hand-built Chance / Community Chest deck — standard UK Monopoly cards that map to
/// the implemented action types (Money / Movement / Jail). Resolve-on-draw unless noted. Replaced
/// by the JSON import once the card system is complete (import is the last step). Group / action
/// ids are left unset — the import / <c>PersistedCardIds</c> stamps those; the resolve-on-draw
/// path doesn't read them (single-group cards never open the option prompt).
///
/// Notes: <c>EachPlayer</c> money cards and the "Get Out of Jail Free" keep cards are correct data
/// but no-op for now — their handlers (the per-player loop, held-card trigger evaluation) aren't
/// wired yet. "Advance to nearest" resolves normal rent (no rent-doubling).
/// </summary>
public static class TEMP_CARDS
{
    public static List<CardModel> LIST = new()
    {
        // ─────────────────────────── CHANCE ───────────────────────────
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Advance to Go (Collect £200)",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.AdvanceToIndex, TargetIndex = 0 }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Advance to Trafalgar Square. If you pass Go, collect £200",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.AdvanceToIndex, TargetIndex = 24 }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Advance to Mayfair",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.AdvanceToIndex, TargetIndex = 39 }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Advance to Pall Mall. If you pass Go, collect £200",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.AdvanceToIndex, TargetIndex = 11 }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Take a trip to Marylebone Station. If you pass Go, collect £200",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.AdvanceToIndex, TargetIndex = 15 }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Advance to the nearest Station",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.AdvanceToNearest, Nearest = NearestKind.Station }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Advance to the nearest Utility",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.AdvanceToNearest, Nearest = NearestKind.Utility }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Go back 3 spaces",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.MoveSpaces, Spaces = -3 }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Go to Jail. Go directly to Jail. Do not pass Go, do not collect £200",
            Groups = [new CardGroup { Actions = [new JailAction { Kind = JailKind.SendToJail }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Bank pays you dividend of £50",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 50, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Your building loan matures. Collect £150",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 150, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Speeding fine £15",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 15, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Make general repairs on all your property. For each house pay £25, for each hotel pay £100",
            Groups = [new CardGroup { Actions = [
                new MoneyAction { Amount = 25, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PerUnit = MoneyPerUnit.PerHouse },
                new MoneyAction { Amount = 100, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PerUnit = MoneyPerUnit.PerHotel }
            ] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "You have been elected Chairman of the Board. Pay each player £50",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 50, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.EachPlayer }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Get Out of Jail Free. This card may be kept until needed",
            ConditionType = CardConditionType.ChoiceCardholderTurn,
            Conditions = [new CardCondition { Trigger = CardTrigger.OnInJail }],
            Groups = [new CardGroup { Actions = [new JailAction { Kind = JailKind.Release }] }]
        },

        // ─────────────────────── COMMUNITY CHEST ───────────────────────
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Advance to Go (Collect £200)",
            Groups = [new CardGroup { Actions = [new MovementAction { Kind = MovementKind.AdvanceToIndex, TargetIndex = 0 }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Go to Jail. Go directly to Jail. Do not pass Go, do not collect £200",
            Groups = [new CardGroup { Actions = [new JailAction { Kind = JailKind.SendToJail }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Bank error in your favour. Collect £200",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 200, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "From sale of stock you get £50",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 50, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Holiday fund matures. Receive £100",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 100, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Life insurance matures. Collect £100",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 100, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Income tax refund. Collect £20",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 20, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Receive £25 consultancy fee",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 25, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "You inherit £100",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 100, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "You have won second prize in a beauty contest. Collect £10",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 10, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Doctor's fee. Pay £50",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 50, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Pay hospital fees of £100",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 100, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Pay school fees of £50",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 50, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "You are assessed for street repairs. £40 per house, £115 per hotel",
            Groups = [new CardGroup { Actions = [
                new MoneyAction { Amount = 40, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PerUnit = MoneyPerUnit.PerHouse },
                new MoneyAction { Amount = 115, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PerUnit = MoneyPerUnit.PerHotel }
            ] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "It is your birthday. Collect £10 from every player",
            Groups = [new CardGroup { Actions = [new MoneyAction { Amount = 10, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.EachPlayer }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Get Out of Jail Free. This card may be kept until needed",
            ConditionType = CardConditionType.ChoiceCardholderTurn,
            Conditions = [new CardCondition { Trigger = CardTrigger.OnInJail }],
            Groups = [new CardGroup { Actions = [new JailAction { Kind = JailKind.Release }] }]
        }
    };
}