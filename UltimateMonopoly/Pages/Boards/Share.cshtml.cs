using JC.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.ViewModels.BoardSkins;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services.BoardSkins;
using UltimateMonopoly.Services.Friends;

namespace UltimateMonopoly.Pages.Boards;

[Authorize]
public class Share : PageModel
{
    private readonly BoardSkinService _boardSkins;
    private readonly BoardSkinShareService _shareService;
    private readonly FriendService _friendService;
    private readonly IUserInfo _userInfo;

    public Share(BoardSkinService boardSkins,
        BoardSkinShareService shareService,
        FriendService friendService,
        IUserInfo userInfo)
    {
        _boardSkins = boardSkins;
        _shareService = shareService;
        _friendService = friendService;
        _userInfo = userInfo;
    }

    public string Id { get; private set; } = string.Empty;
    public BoardSkinViewModel? Skin { get; private set; }
    public List<FriendViewModel> Friends { get; private set; } = [];
    public HashSet<string> SharedFriendIds { get; private set; } = [];

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        Skin = await _boardSkins.GetBoardSkin(id, includeSpaces: false);
        if (Skin is null) return NotFound();

        Id = id;
        Friends = await _friendService.GetFriendsList();
        SharedFriendIds = (await _shareService.GetUserIdsForSharedBoardSkin(id)).ToHashSet();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? id, List<string>? friendIds)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var skin = await _boardSkins.GetBoardSkin(id, includeSpaces: false);
        if (skin is null) return NotFound();

        var ok = await _shareService.TryShareBoardSkin(id, friendIds ?? []);

        StatusMessage = ok 
            ? "Board sharing saved." 
            : _userInfo.IsInRole(AppRoles.Restricted) 
                ? "Your account is restricted and cannot share boards with new users. You can only remove existing board shares with friends."
                : "Could not save board sharing.";
        
        
        StatusKind = ok ? "success" : "danger";
        return RedirectToPage(new { id });
    }
}