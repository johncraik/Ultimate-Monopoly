using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Abstractions;

public interface ITurnTaxService
{
    bool Enabled { get; }
    Task Import();
    
    Task ApplyTax(Services.Framework.GameEngine engine, PlayerModel player, CancellationToken ct);
    
    uint TotalTax(uint balance);
}