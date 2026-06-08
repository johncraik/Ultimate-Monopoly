using MP.GameEngine.Enums;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Abstractions;

public interface IShortfallService
{
    Task<ShortfallOutcome> ResolveShortfall(
        Services.Framework.GameEngine engine,
        PlayerModel player,
        uint amountOwed,
        string? owedToPlayerId,
        ushort? counterpartyPropertyIndex,
        CancellationToken ct);
}