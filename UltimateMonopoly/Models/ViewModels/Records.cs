using MP.GameEngine.Enums;
using UltimateMonopoly.Enums;
using UltimateMonopoly.Models.ViewModels.BoardSkins;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Models.ViewModels;

//Social Records:
public record FriendRequestResult(bool Success, string? ErrorMessage);
public record ChatMessage(bool FromMe, string AuthorDisplay, string Body, DateTime SentUtc);

// GameId set → the (non-setup) View button opens that player's game stats for the game;
// null → it opens the player's social profile.
public record PlayerCard(ushort OrderId, UserProfileViewModel Profile, ushort? Dice1, ushort? Dice2, bool IsHost, bool IsSetup, string? GameId = null);


//Board Skin Editor Records:
public record SpaceSection(string Key, string Title, string? BannerClass, SpaceShape Shape, List<SpaceCard> Spaces);
public record SpaceCard(ushort Index, string DefaultName, BoardSkinSpaceViewModel? Custom)
{
    public string DisplayName => Custom?.Name ?? DefaultName;
    public bool IsCustomised => Custom is not null;
}

public record SaveSkinResult(bool Success, string? Id);


//Game Setup Records:
public record GameCreationResult(bool Result, string? GameId = null);
public record JoinGameResult(bool Result, string? Message = null, string? GameId = null);


//Profanity (B1):
// MatchedTerm is the offending term (friendly error + audit); Source is which layer caught it.
public record ProfanityResult(bool IsProfane, string? MatchedTerm, ProfanitySource Source)
{
    public static readonly ProfanityResult Clean = new(false, null, ProfanitySource.None);
}








