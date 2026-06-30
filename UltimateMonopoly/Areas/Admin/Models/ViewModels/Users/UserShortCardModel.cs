namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;

/// <summary>Backs the <c>_UserShortCard</c> partial — a compact user summary (name, email, state) +
/// a link to the full Details page. <c>User</c> is null when the account has been deleted, in which case
/// the raw <c>UserId</c> is shown instead.</summary>
public class UserShortCardModel
{
    public string Title { get; }
    public UserViewModel? User { get; }
    public string UserId { get; }

    public UserShortCardModel(string title, UserViewModel? user, string userId)
    {
        Title = title;
        User = user;
        UserId = userId;
    }
}