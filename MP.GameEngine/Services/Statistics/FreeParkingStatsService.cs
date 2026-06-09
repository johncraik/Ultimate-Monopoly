using System.Text.Json;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

/// <summary>
/// Free Parking property stats (<c>game-stats.md</c> §13.7): how many properties the
/// player handed in / took from the pot, and which set types they've handed in. The
/// money side (pot take/pay) lives in the income/spending services; landings live in
/// movement.
/// </summary>
public class FreeParkingStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        var playerId = player.PlayerId;

        // Every FP hand-in / take is a PropertyTransferReceipt with reason FreeParking:
        // Value < 0 = handed a property into the pot, Value > 0 = took one out.
        var fpTransfers = snapshot.Turns
            .SelectMany(t => t.Events.OfType<PropertyTransferReceipt>())
            .Where(r => r.PlayerId == playerId && r.Reason == PropertyTransferReason.FreeParking)
            .ToList();

        record.TotalPropertiesHandedInFP = (uint)fpTransfers.Where(r => r.Value < 0).Sum(r => -r.Value);
        record.TotalPropertiesTakenFromFP = (uint)fpTransfers.Where(r => r.Value > 0).Sum(r => r.Value);

        // Set types handed into Free Parking — FreeParkingService only ever appends to
        // FPHandedInSets (a hand-in marks that whole set used for the game), so the final
        // snapshot holds the complete list. Null when the player handed in nothing.
        var handedInSets = snapshot.Turns.LastOrDefault()?.Game.Players
            .FirstOrDefault(p => p.PlayerId == playerId)?.FPHandedInSets ?? [];

        record.FPHandedInSetTypesJson = handedInSets.Count > 0
            ? JsonSerializer.Serialize(handedInSets)
            : null;

        return record;
    }
}