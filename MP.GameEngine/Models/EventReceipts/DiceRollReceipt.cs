using System.Text.Json.Serialization;
using MP.GameEngine.Enums;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.EventReceipts;

public class DiceRollReceipt : EventReceipt
{
    public ushort Die1 { get; init; }
    public ushort? Die2 { get; init; }
    public ushort? ThirdDie { get; init; }
    
    public DiceRollType RollType { get; init; }
    
    [JsonIgnore]
    public bool IsTurnRoll => Die2 != null && ThirdDie != null;

    public DiceRollReceipt(string playerId, DiceRoll dice)
    {
        PlayerId = playerId;
        Die1 = dice.Die1;
        Die2 = dice.Die2;
        ThirdDie = dice.ThirdDie;
        RollType = dice.RollType;
    }
}