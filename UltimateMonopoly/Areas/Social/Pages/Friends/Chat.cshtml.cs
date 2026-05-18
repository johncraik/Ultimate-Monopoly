using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UltimateMonopoly.Areas.Social.Pages.Friends;

public class ChatModel : PageModel
{
    public string FriendUsername { get; private set; } = "";
    public string FriendDisplayName { get; private set; } = "";
    public string FriendInitials { get; private set; } = "??";
    public string FriendAvatarColor { get; private set; } = "secondary";
    public bool FriendOnline { get; private set; }

    public List<ChatMessage> Messages { get; private set; } = [];

    public void OnGet(string friend)
    {
        FriendUsername = friend ?? "";

        // Dummy lookup — pretend we resolved the friend from the username.
        (FriendDisplayName, FriendInitials, FriendAvatarColor, FriendOnline) = friend switch
        {
            "alice99"   => ("Alice Henderson", "AH", "purple", true),
            "bobby_b"   => ("Bobby Brennan",   "BB", "teal",   true),
            "charliem"  => ("Charlie Mason",   "CM", "indigo", false),
            "dee_v"     => ("Dee Vargas",      "DV", "pink",   true),
            "evgeniya"  => ("Evgeniya Sokol",  "ES", "orange", false),
            "fraz"      => ("Fraser Quinn",    "FQ", "green",  false),
            "gigi"      => ("Gigi Park",       "GP", "blue",   true),
            "hank.t"    => ("Hank Tomlin",     "HT", "red",    false),
            _           => (friend ?? "Unknown", "??", "secondary", false)
        };

        // Dummy thread.
        var now = DateTime.UtcNow;
        Messages =
        [
            new(FromMe: false, FriendDisplayName, "gg last game, you crushed me on Boardwalk", now.AddMinutes(-32)),
            new(FromMe: true,  "You",             "ha thanks — those three houses were brutal to pay off though",  now.AddMinutes(-31)),
            new(FromMe: false, FriendDisplayName, "rematch? speed edition this time",                              now.AddMinutes(-20)),
            new(FromMe: true,  "You",             "sure, give me 10 mins",                                         now.AddMinutes(-19)),
            new(FromMe: false, FriendDisplayName, "👍",                                                            now.AddMinutes(-18))
        ];
    }

    public record ChatMessage(bool FromMe, string AuthorDisplay, string Body, DateTime SentUtc);
}
