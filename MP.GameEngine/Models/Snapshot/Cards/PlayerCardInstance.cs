using MP.GameEngine.Models.Cards;

namespace MP.GameEngine.Models.Snapshot.Cards;

public class PlayerCardInstance
{
    public string CardId { get; set; }
    public string? ChosenGroupId { get; set; }
    public ushort? TurnsRemaining { get; set; }
    public uint? DrawnOnTurn { get; set; }

    public PlayerCardInstance()
    {
    }

    public PlayerCardInstance(PlayerCardInstance instance)
    {
        CardId = instance.CardId;
        ChosenGroupId = instance.ChosenGroupId;
        TurnsRemaining = instance.TurnsRemaining;
        DrawnOnTurn = instance.DrawnOnTurn;
    }

    public PlayerCardInstance(CardModel card)
    {
        CardId = card.CardId;
        DrawnOnTurn = card.DrawnOnTurn;
    }

    public void UpdateInstance(CardModel card, CardGroup chosenGroup)
    {
        DrawnOnTurn = card.DrawnOnTurn;
        ChosenGroupId = chosenGroup.IsChosenGroup ? chosenGroup.GroupId : null;
        TurnsRemaining = chosenGroup.TurnsRemaining;
    }
}