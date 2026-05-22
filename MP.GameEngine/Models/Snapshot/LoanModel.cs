using System.Text.Json.Serialization;

namespace MP.GameEngine.Models.Snapshot;

public class LoanModel
{
    public uint Amount { get; set; }
    public uint PaidBack { get; set; }
    
    [JsonIgnore]
    public bool IsOutstanding => PaidBack < Amount;
}