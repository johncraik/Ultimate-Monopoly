using System.Text.Json.Nodes;

namespace UltimateMonopoly.Areas.Admin.Models;

/// <summary>One turn's combined export — the snapshot and events embedded as nested JSON (parsed from
/// their stored strings, so the download is clean, inspectable JSON rather than escaped strings).</summary>
public record GameTurnExport(
    uint TurnNumber,
    string TurnId,
    string CurrentPlayerId,
    string? CurrentPlayerName,
    bool IsFinalTurn,
    JsonNode? Snapshot,
    JsonNode? Events);

/// <summary>The whole-game export — a small header plus every turn, for human/AI inspection.</summary>
public record GameExport(
    string GameId,
    string Name,
    string State,
    string Outcome,
    string HostUserId,
    IReadOnlyList<GameTurnExport> Turns);