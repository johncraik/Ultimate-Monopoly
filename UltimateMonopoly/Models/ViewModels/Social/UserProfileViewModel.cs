using UltimateMonopoly.Data;

namespace UltimateMonopoly.Models.ViewModels.Social;

public class UserProfileViewModel
{
    public string UserId { get; }
    public string Username { get; }
    public string DisplayName { get; }
    public string Initial { get; }

    public string? AvatarColour { get; }
    public string? AvatarImageUrl { get; }
    
    public uint NumberOfWins { get; }
    public uint NumberOfLosses { get; }
    public uint NumberOfDraws { get; }
    public uint NumberOfGamesPlayed => NumberOfWins + NumberOfLosses + NumberOfDraws;

    public UserProfileViewModel(uint numberOfWins, uint numberOfLosses, uint numberOfDraws, bool hideStats = false)
    {
        UserId = string.Empty;
        Username = "user";
        DisplayName = "User";
        Initial = "U";
        
        if (hideStats) return;
        
        NumberOfWins = numberOfWins;
        NumberOfLosses = numberOfLosses;
        NumberOfDraws = numberOfDraws;
    }
    
    public UserProfileViewModel(string userId, string? username, string? displayName,
        string? avatarColour, string? avatarImageUrl)
    {
        UserId = userId;
        Username = string.IsNullOrWhiteSpace(username) ? "Unknown" : username;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Username : displayName;
        Initial = DisplayName.Length > 0 ? $"{DisplayName[0]}".ToUpperInvariant() : "U";

        AvatarColour = avatarColour;
        AvatarImageUrl = avatarImageUrl;
    }

    public UserProfileViewModel(AppUser user, string? imgUrl)
        : this(user.Id, user.UserName, user.DisplayName, user.AvatarColour, imgUrl)
    {
        NumberOfWins = user.NumberOfWins;
        NumberOfLosses = user.NumberOfLosses;
        NumberOfDraws = user.NumberOfDraws;
    }
}