using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.EventReceipts;

public class PlayerBankruptedReceipt : EventReceipt
{
    public uint PlayerBalance { get; init; }
    public uint? BankruptAmountBy { get; init; }
    public bool VoluntaryBankruptcy { get; init; }
    
    [JsonIgnore]
    public uint? ShortfallAmount => BankruptAmountBy - PlayerBalance;
}