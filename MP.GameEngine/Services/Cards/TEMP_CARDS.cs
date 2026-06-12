using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Cards.Conditions;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Services.Cards;

/// <summary>
/// Temporary hand-built Chance / Community Chest deck — a slim, deliberately varied set that
/// exercises every implemented action path (Money / Movement / Jail) rather than the full standard
/// UK deck. Replaced by the JSON import once the card system is complete (import is the last step).
/// Group / action ids are left unset — the import / <c>PersistedCardIds</c> stamps those; the
/// resolve-on-draw path doesn't read them.
///
/// <para>Card text uses the dynamic cost-tag format (<see cref="Helpers.Cards.CardDisplayHelper"/>):
/// a <c>{G&lt;groupIndex&gt;__&lt;actionIndex&gt;}</c> tag in the card text (and a bare
/// <c>{&lt;actionIndex&gt;}</c> in a group's text) is replaced at display time with the matching
/// <see cref="MoneyAction"/> amount, after the game's rounding (and the %cap for a percentage
/// action). Each group's <see cref="CardGroup.GroupKey"/> is stamped <c>G&lt;index&gt;</c> to match,
/// the same key <c>CardImportService</c> assigns. Per-unit and dice-multiplier amounts display their
/// raw base (the realised product isn't known until resolve). Flavour amounts with no money action
/// behind them (the GO bonus) stay literal.</para>
///
/// <para>Demonstrated paths: bank/FP pay-receive, per-house/per-hotel charges, percentage cards,
/// each-player charges, the highest/lowest-roller dice-off, the one/two-die multiplier, advance /
/// move / nearest / swap movement (incl. a "do not collect from GO" move), jail send/release, and a
/// two-group OR-choice (the option prompt + dynamic group text). The "Get Out of Jail Free" keep
/// cards still wait on held-card trigger evaluation.</para>
/// </summary>
public static class TEMP_CARDS
{
    public static List<CardModel> LIST = new()
    {
        // ─────────────────────────── CHANCE ───────────────────────────
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Advance to Go (Collect £200)",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MovementAction { Kind = MovementKind.AdvanceToIndex, TargetIndex = 0 }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Advance to the nearest Station",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MovementAction { Kind = MovementKind.AdvanceToNearest, Nearest = NearestKind.Station }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Go back 3 spaces",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MovementAction { Kind = MovementKind.MoveSpaces, Spaces = -3 }] }]
        },
        new CardModel
        {
            // MoveSpaces with CollectGoBonus = false — you move (and act on the landed space) but
            // collect nothing for passing GO.
            CardType = CardType.Chance, CardText = "In a hurry! Dash forward 9 spaces — too rushed to collect anything from Go",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MovementAction { Kind = MovementKind.MoveSpaces, Spaces = 9, CollectGoBonus = false }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Make general repairs on all your property. For each house pay {G0__0}, for each hotel pay {G0__1}",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [
                new MoneyAction { Amount = 25, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PerUnit = MoneyPerUnit.PerHouse },
                new MoneyAction { Amount = 100, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PerUnit = MoneyPerUnit.PerHotel }
            ] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "You have been elected Chairman of the Board. Pay each player {G0__0}",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MoneyAction { Amount = 50, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.EachPlayer }] }]
        },
        new CardModel
        {
            // HighestRoller dice-off with the holder rolling too (IncludeHolderInRoll): everyone rolls
            // one die; if the holder wins, they pay nothing (the movement would be with themselves).
            CardType = CardType.Chance, CardText = "Robbery! Everyone (you included) rolls one die. Pay {G0__0} to the highest roller — roll high to keep your money",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MoneyAction { Amount = 100, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.HighestRoller, IncludeHolderInRoll = true }] }]
        },
        new CardModel
        {
            // One-die multiplier: roll one die, collect £100 × the roll from the bank.
            CardType = CardType.Chance, CardText = "Lucky dip! Roll one die and collect {G0__0} times the roll",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MoneyAction { Amount = 100, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank, DiceMultiplier = DiceMultiplier.OneDie }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Get Out of Jail Free. This card may be kept until needed",
            ConditionType = CardConditionType.ChoiceCardholderTurn,
            Conditions = [new CardCondition { Trigger = CardTrigger.OnInJail }],
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new JailAction { Kind = JailKind.Release }] }]
        },
        new CardModel
        {
            CardType = CardType.Chance, CardText = "Go to Jail. Go directly to Jail. Do not pass Go, do not collect £200",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new JailAction { Kind = JailKind.SendToJail }] }]
        },

        // ─────────────────────── COMMUNITY CHEST ───────────────────────
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Advance to Go (Collect £200)",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MovementAction { Kind = MovementKind.AdvanceToIndex, TargetIndex = 0 }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Bank error in your favour. Collect {G0__0}",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MoneyAction { Amount = 200, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "You are assessed for street repairs. {G0__0} per house, {G0__1} per hotel",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [
                new MoneyAction { Amount = 40, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PerUnit = MoneyPerUnit.PerHouse },
                new MoneyAction { Amount = 115, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PerUnit = MoneyPerUnit.PerHotel }
            ] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "It is your birthday. Collect {G0__0} from every player",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MoneyAction { Amount = 10, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.EachPlayer }] }]
        },
        new CardModel
        {
            // LowestRoller dice-off: the other players each roll one die; you collect from the lowest.
            CardType = CardType.ComChest, CardText = "Tax rebate! Collect {G0__0} from the player who rolls the lowest with one die",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MoneyAction { Amount = 50, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.LowestRoller }] }]
        },
        new CardModel
        {
            // Two-die multiplier: roll two dice, collect £100 × the total from the bank.
            CardType = CardType.ComChest, CardText = "Investment windfall! Roll two dice and collect {G0__0} times the total",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MoneyAction { Amount = 100, Direction = MoneyDirection.Receive, Counterparty = MoneyCounterparty.Bank, DiceMultiplier = DiceMultiplier.TwoDice }] }]
        },
        new CardModel
        {
            // Percentage card: the £2000 base scales by the %cap (100/50/10) at both display and resolve.
            CardType = CardType.ComChest, CardText = "Property tax assessment. Pay {G0__0} into Free Parking",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MoneyAction { Amount = 2000, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking, PercentageApplies = true }] }]
        },
        new CardModel
        {
            // Swap: exchange board positions with a chosen player (no GO bonus, no landed-space action).
            CardType = CardType.ComChest, CardText = "ID check fails! Swap places with a player of your choice",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new MovementAction { Kind = MovementKind.Swap, Target = PlayerTarget.ChosenPlayer }] }]
        },
        new CardModel
        {
            // Two groups = an OR-choice (opens CardOptionPrompt). Option labels render from each
            // group's text; the body renders from the card text — both dynamic.
            CardType = CardType.ComChest, CardText = "Council fine. Pay {G0__0} into Free Parking, or go back 3 spaces",
            Groups =
            [
                new CardGroup { GroupKey = "G0", GroupText = "Pay {0} into Free Parking",
                    Actions = [new MoneyAction { Amount = 100, Direction = MoneyDirection.Pay, Counterparty = MoneyCounterparty.FreeParking }] },
                new CardGroup { GroupKey = "G1", GroupText = "Go back 3 spaces",
                    Actions = [new MovementAction { Kind = MovementKind.MoveSpaces, Spaces = -3 }] }
            ]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Get Out of Jail Free. This card may be kept until needed",
            ConditionType = CardConditionType.ChoiceCardholderTurn,
            Conditions = [new CardCondition { Trigger = CardTrigger.OnInJail }],
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new JailAction { Kind = JailKind.Release }] }]
        },
        new CardModel
        {
            CardType = CardType.ComChest, CardText = "Go to Jail. Go directly to Jail. Do not pass Go, do not collect £200",
            Groups = [new CardGroup { GroupKey = "G0", Actions = [new JailAction { Kind = JailKind.SendToJail }] }]
        }
    };
}