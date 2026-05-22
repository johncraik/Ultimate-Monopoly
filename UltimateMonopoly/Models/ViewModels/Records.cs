using MP.GameEngine.Enums;
using UltimateMonopoly.Models.ViewModels.BoardSkins;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Models.ViewModels;

//Social Records:
public record FriendRequestResult(bool Success, string? ErrorMessage);
public record ChatMessage(bool FromMe, string AuthorDisplay, string Body, DateTime SentUtc);

public record PlayerCard(ushort OrderId, UserProfileViewModel Profile, ushort? Dice1, ushort? Dice2, bool IsHost);


//Board Skin Editor Records:
public record SpaceSection(string Key, string Title, string? BannerClass, SpaceShape Shape, List<SpaceCard> Spaces);
public record SpaceCard(ushort Index, string DefaultName, BoardSkinSpaceViewModel? Custom)
{
    public string DisplayName => Custom?.Name ?? DefaultName;
    public bool IsCustomised => Custom is not null;
}

public record SaveSkinResult(bool Success, string? Id);


//Game Setup Records:
public record GameCreationResult(bool Result, string? GameId = null, string? JoinQrCode = null);
public record JoinGameResult(bool Result, string? Message = null, string? GameId = null);








