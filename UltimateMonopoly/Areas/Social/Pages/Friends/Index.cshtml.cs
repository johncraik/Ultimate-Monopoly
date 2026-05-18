using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UltimateMonopoly.Areas.Social.Pages.Friends;

public class IndexModel : PageModel
{
    // Dummy data — no services, repositories or context wired yet.
    private static readonly string[] KnownUsernames = ["alice", "bob", "charlie", "david", "eve"];

    public List<Friend> Friends { get; } =
    [
        new("alice99",   "Alice Henderson", "AH", "purple",  true,  "Atlantic City Marathon", DateTime.UtcNow.AddMinutes(-3)),
        new("bobby_b",   "Bobby Brennan",   "BB", "teal",    true,  "Boardwalk Blitz",        DateTime.UtcNow.AddMinutes(-12)),
        new("charliem",  "Charlie Mason",   "CM", "indigo",  false, "Classic Monopoly",       DateTime.UtcNow.AddHours(-2)),
        new("dee_v",     "Dee Vargas",      "DV", "pink",    true,  "Speed Edition",          DateTime.UtcNow.AddMinutes(-44)),
        new("evgeniya",  "Evgeniya Sokol",  "ES", "orange",  false, "Atlantic City Marathon", DateTime.UtcNow.AddDays(-1)),
        new("fraz",      "Fraser Quinn",    "FQ", "green",   false, "Junior",                 DateTime.UtcNow.AddDays(-3)),
        new("gigi",      "Gigi Park",       "GP", "blue",    true,  "Boardwalk Blitz",        DateTime.UtcNow.AddMinutes(-1)),
        new("hank.t",    "Hank Tomlin",     "HT", "red",     false, "Cheaters Edition",       DateTime.UtcNow.AddDays(-9))
    ];

    public List<FriendRequest> IncomingRequests { get; } =
    [
        new("ivor.t",  "Ivor Trent",     "IT", "indigo", DateTime.UtcNow.AddHours(-3)),
        new("jules_w", "Jules Whitcomb", "JW", "teal",   DateTime.UtcNow.AddDays(-2)),
        new("kelpy",   "Kelpy Mira",     "KM", "orange", DateTime.UtcNow.AddDays(-6))
    ];

    public List<FriendRequest> OutgoingRequests { get; } =
    [
        new("luca99", "Luca Romano", "LR", "pink",  DateTime.UtcNow.AddHours(-6)),
        new("mia.b",  "Mia Bishop",  "MB", "green", DateTime.UtcNow.AddDays(-1))
    ];

    [BindProperty]
    public AddFriendInput Input { get; set; } = new();

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public string Tab { get; private set; } = "friends";

    public void OnGet(string? tab = null)
    {
        Tab = NormalizeTab(tab);
    }

    public IActionResult OnPostAddFriend()
    {
        if (!ModelState.IsValid)
        {
            Tab = "add";
            return Page();
        }

        var username = Input.Username.Trim();
        var found = KnownUsernames.Contains(username, StringComparer.OrdinalIgnoreCase);

        StatusMessage = found
            ? $"Friend request sent to {username}."
            : $"No user found with the username '{username}'.";
        StatusKind = found ? "success" : "danger";

        return RedirectToPage(new { tab = "add" });
    }

    private static string NormalizeTab(string? tab) => tab switch
    {
        "requests" => "requests",
        "add"      => "add",
        _          => "friends"
    };

    public record Friend(
        string Username,
        string DisplayName,
        string Initials,
        string AvatarColor,
        bool IsOnline,
        string LastPlayedGame,
        DateTime LastSeenUtc);

    public record FriendRequest(
        string Username,
        string DisplayName,
        string Initials,
        string AvatarColor,
        DateTime RequestedAtUtc);

    public class AddFriendInput
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter a username.")]
        [System.ComponentModel.DataAnnotations.StringLength(64, MinimumLength = 2)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Username")]
        public string Username { get; set; } = "";
    }
}
