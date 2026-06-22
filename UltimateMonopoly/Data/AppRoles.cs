using JC.Identity.Authentication;

namespace UltimateMonopoly.Data;

public class AppRoles : SystemRoles
{
    public const string Restricted = nameof(Restricted);
    public const string RestrictedDesc = "Restricted user that cannot send friend requests, messages, create games, create or share board skins.";
    
    public const string HiddenUser = nameof(HiddenUser);
    public const string HiddenUserDesc = "User that is not publicly visible. Other users can still add them to their friends list to see their profile.";
}
