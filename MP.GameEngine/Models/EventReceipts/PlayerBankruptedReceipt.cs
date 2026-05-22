using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.EventReceipts;

public class PlayerBankruptedReceipt : EventReceipt
{
    public uint PlayerBalance { get; set; }
    public uint? BankruptAmountBy { get; set; }
    public bool VoluntaryBankruptcy { get; set; }
    
    [JsonIgnore]
    public uint? ShortfallAmount => BankruptAmountBy - PlayerBalance;
}