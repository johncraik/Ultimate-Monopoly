using System.ComponentModel.DataAnnotations;
using JC.Core.Extensions;
using JC.Web.UI.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using UltimateMonopoly.Models.DataModels.Social;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services.Friends;

namespace UltimateMonopoly.Areas.Social.Pages.Friends;

public class IndexModel : PageModel
{
    private readonly FriendService _friendService;
    private readonly BlockAndReportService _blockAndReport;

    public IndexModel(FriendService friendService, BlockAndReportService blockAndReport)
    {
        _friendService = friendService;
        _blockAndReport = blockAndReport;
    }

    public List<FriendViewModel> Friends { get; private set; } = [];
    public IReadOnlyList<FriendRequestViewModel> IncomingRequests { get; private set; } = [];
    public IReadOnlyList<FriendRequestViewModel> OutgoingRequests { get; private set; } = [];

    public List<SelectListItem> ReportReasons { get; private set; } = [];

    [BindProperty]
    public AddFriendInput Input { get; set; } = new();

    [BindProperty]
    public ReportInputModel Report { get; set; } = new();

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public string Tab { get; private set; } = "friends";

    public async Task OnGetAsync(string? tab = null)
    {
        Tab = NormaliseTab(tab);
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddFriendAsync()
    {
        // Ignore Report.* validation entries — Report isn't on this form.
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Report.")).ToList())
            ModelState.Remove(key);

        if (!ModelState.IsValid)
        {
            Tab = "add";
            await LoadAsync();
            return Page();
        }

        var username = Input.Username.Trim();
        var result = await _friendService.TrySendFriendRequest(username);

        StatusMessage = result.Success
            ? $"Friend request sent to {username}."
            : result.ErrorMessage ?? "Could not send friend request.";
        StatusKind = result.Success ? "success" : "danger";

        return RedirectToPage(new { tab = "add" });
    }

    public async Task<IActionResult> OnPostAcceptAsync(string requestId)
    {
        var ok = await _friendService.TryAcceptFriendRequest(requestId);
        StatusMessage = ok ? "Friend request accepted." : "Could not accept friend request.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { tab = "requests" });
    }

    public async Task<IActionResult> OnPostDeclineAsync(string requestId)
    {
        var ok = await _friendService.TryDeclineFriendRequest(requestId);
        StatusMessage = ok ? "Friend request declined." : "Could not decline friend request.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { tab = "requests" });
    }

    public async Task<IActionResult> OnPostCancelAsync(string requestId)
    {
        var ok = await _friendService.TryCancelFriendRequest(requestId);
        StatusMessage = ok ? "Friend request cancelled." : "Could not cancel friend request.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { tab = "requests" });
    }

    public async Task<IActionResult> OnPostRemoveAsync(string userId)
    {
        var ok = await _friendService.TryRemoveFriend(userId);
        StatusMessage = ok ? "Friend removed." : "Could not remove friend.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { tab = "friends" });
    }

    public async Task<IActionResult> OnPostBlockAsync(string userId)
    {
        var ok = await _blockAndReport.TryBlockUser(userId);
        StatusMessage = ok ? "User blocked." : "Could not block user.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { tab = "friends" });
    }

    public async Task<IActionResult> OnPostReportAsync(string userId)
    {
        // Don't gate on ModelState.IsValid — the AddFriend form's Required/StringLength
        // attributes on Input.Username trip on this form's POST (since Username isn't
        // in the report form) and that's unrelated to whether the report is valid.
        if (Report.Reason is null)
        {
            StatusMessage = "Please select a reason for the report.";
            StatusKind = "danger";
            return RedirectToPage(new { tab = "friends" });
        }

        var ok = await _blockAndReport.TryBlockAndReport(
            userId,
            new ReportInput(Report.Reason.Value, Report.Message));

        StatusMessage = ok ? "User reported and blocked." : "Could not report user.";
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { tab = "friends" });
    }

    private async Task LoadAsync()
    {
        Friends = await _friendService.GetFriendsList();
        var requests = await _friendService.GetFriendRequests();
        IncomingRequests = requests.IncomingRequests;
        OutgoingRequests = requests.OutgoingRequests;

        ReportReasons = DropdownHelper.FromCollection(
            Enum.GetValues<ReportReason>(),
            r => r.GetDescription(),
            r => r.ToString());
    }

    private static string NormaliseTab(string? tab) => tab switch
    {
        "requests" => "requests",
        "add"      => "add",
        _          => "friends"
    };

    public class AddFriendInput
    {
        [Required(ErrorMessage = "Enter a username.")]
        [StringLength(64, MinimumLength = 2)]
        [Display(Name = "Username")]
        public string Username { get; set; } = "";
    }

    public class ReportInputModel
    {
        // No data-annotation attributes — they'd run on every form POST against this
        // page (cross-pollination with Input.Username's Required/StringLength). The
        // <select required> + the OnPostReportAsync null check enforce Reason; the
        // textarea's maxlength + the DB column's [MaxLength(10240)] enforce Message.
        public ReportReason? Reason { get; set; }
        public string? Message { get; set; }
    }
}