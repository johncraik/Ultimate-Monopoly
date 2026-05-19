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

    public UserProfileViewModel(AppUser user, string? imgUrl)
    {
        UserId = user.Id;
        Username = user.UserName ?? "Unknown";
        DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? Username : user.DisplayName;
        Initial = DisplayName.Length > 0 ? $"{DisplayName[0]}".ToUpperInvariant() : "U";

        AvatarColour = user.AvatarColour;
        AvatarImageUrl = imgUrl;
        
        NumberOfWins = user.NumberOfWins;
        NumberOfLosses = user.NumberOfLosses;
        NumberOfDraws = user.NumberOfDraws;
    }
}