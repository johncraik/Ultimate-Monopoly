using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels.Social;

namespace UltimateMonopoly.Models.ViewModels.Social;

public class FriendRequestViewModel : UserProfileViewModel
{
    public string RequestId { get; }
    public bool IsOutgoing { get; }
    
    public string RequestDate { get; }

    public FriendRequestViewModel(string userId, FriendRequest request, AppUser user, string? imgUrl)
        : base(user, imgUrl)
    {
        RequestId = request.Id;
        IsOutgoing = string.Equals(userId, request.CreatedById);
        RequestDate = request.CreatedUtc.ToLocalTime().ToString("D");
    }
}