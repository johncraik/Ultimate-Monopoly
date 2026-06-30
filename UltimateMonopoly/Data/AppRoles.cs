using JC.Identity.Authentication;

namespace UltimateMonopoly.Data;

public class AppRoles : SystemRoles
{
    public const string Restricted = nameof(Restricted);
    public const string RestrictedDesc = "Restricts this user from sending friend requests, messages, creating games, or creating/sharing board skins.";
    
    public const string HiddenUser = nameof(HiddenUser);
    public const string HiddenUserDesc = "User that is not publicly visible. Other users can still add them to their friends list to see their profile.";
    
    public const string GithubManager  = nameof(GithubManager);
    public const string GithubManagerDesc = "Sends notifications to the user for new and updated Github issues.";
}
