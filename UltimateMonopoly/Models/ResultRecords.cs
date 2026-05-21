namespace UltimateMonopoly.Models;

public record GameCreationResult(bool Result, string? GameId = null, string? JoinQrCode = null);

public record JoinGameResult(bool Result, string? Message = null, string? GameId = null);

public record FriendRequestResult(bool Success, string? ErrorMessage);

public record SaveSkinResult(bool Success, string? Id);