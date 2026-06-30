using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.EventReceipts;

public class PlayerBankruptedReceipt : EventReceipt
{
    public uint PlayerBalance { get; init; }
    public uint? BankruptAmountBy { get; init; }
    public bool VoluntaryBankruptcy { get; init; }
    
    // Clamp by comparing before the subtraction: uint − uint wraps to a huge value if the balance
    // exceeds the debt, and Math.Max can't undo a wrap that already happened (L-05). Null debt → null.
    [JsonIgnore]
    public uint? ShortfallAmount => BankruptAmountBy is { } amount
        ? (amount > PlayerBalance ? amount - PlayerBalance : 0u)
        : null;
}