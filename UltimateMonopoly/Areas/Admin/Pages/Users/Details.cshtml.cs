using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Data;
using UltimateMonopoly.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Users;

public class DetailsModel : PageModel
{
    private readonly UserManagementService _users;
    private readonly ProfanityService _profanity;
    private readonly IUserInfo _userInfo;

    public DetailsModel(UserManagementService users, ProfanityService profanity, IUserInfo userInfo)
    {
        _users = users;
        _profanity = profanity;
        _userInfo = userInfo;
    }
    
    public string UserId { get; set; } = "";

    [BindProperty]
    public string? DisplayName { get; set; }

    public UserViewModel User { get; private set; } = default!;

    // View gates.
    public bool IsSystemAdmin => _userInfo.IsInRole(SystemRoles.SystemAdmin);
    public bool IsSelf => UserId == _userInfo.UserId;
    public bool TargetIsAdmin => User.Roles.Contains(SystemRoles.Admin);
    public bool TargetIsSystemAdmin => User.Roles.Contains(SystemRoles.SystemAdmin);
    public bool TargetIsRestricted => User.IsRestricted ?? false;
    public bool TargetIsHidden => User.Roles.Contains(AppRoles.HiddenUser);
    // Delete is SystemAdmin-only, never on yourself, never on another SystemAdmin (remove that role first).
    public bool CanDelete => IsSystemAdmin && !IsSelf && !TargetIsSystemAdmin;
    // Mirrors the service guard: a plain Admin can't restrict or disable a SystemAdmin (only SystemAdmins can moderate staff).
    public bool CanModerateTarget => IsSystemAdmin || !TargetIsSystemAdmin;

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        UserId = userId;
        
        var user = await _users.GetUserById(UserId);
        if (user == null) return NotFound();

        User = user;
        DisplayName = user.Profile.DisplayName;
        return Page();
    }

    // ---- Section 1: user-editable ----

    public async Task<IActionResult> OnPostDisplayNameAsync(string userId)
    {
        UserId = userId;
        
        var user = await _users.GetUserById(UserId);
        if (user == null) return NotFound();
        User = user;

        // Same profanity gate as the user's own Manage page — generic message; the term goes to the log, not the UI.
        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            var profanity = await _profanity.Check(DisplayName);
            if (profanity.IsProfane)
                ModelState.AddModelError(nameof(DisplayName), "This display name isn't allowed. Please choose another.");
        }
        if (!ModelState.IsValid) return Page();

        await _users.ChangeDisplayName(UserId, DisplayName);
        TempData["Success"] = "Display name updated.";
        return RedirectToPage(new { userId = UserId });
    }

    public async Task<IActionResult> OnPostToggleHiddenAsync(string userId)
    {
        UserId = userId;
        
        var user = await _users.GetUserById(UserId);
        if (user == null) return NotFound();

        await _users.SetHidden(UserId, !user.Roles.Contains(AppRoles.HiddenUser));
        return RedirectToPage(new { userId = UserId });
    }

    // ---- Section 2: account actions ----

    public async Task<IActionResult> OnPostToggleRestrictedAsync(string userId)
    {
        UserId = userId;
        
        var user = await _users.GetUserById(UserId);
        if (user == null) return NotFound();

        await _users.SetRestricted(UserId, !(user.IsRestricted ?? false));
        return RedirectToPage(new { userId = UserId });
    }

    public async Task<IActionResult> OnPostToggleEnabledAsync(string userId)
    {
        UserId = userId;
        
        var user = await _users.GetUserById(UserId);
        if (user == null) return NotFound();

        // Self-guard: disabling your own account is an immediate lockout.
        if (IsSelf && user.IsEnabled)
        {
            TempData["Error"] = "You cannot disable your own account.";
            return RedirectToPage(new { userId = UserId });
        }

        await _users.SetEnabled(UserId, !user.IsEnabled);
        return RedirectToPage(new { userId = UserId });
    }

    public async Task<IActionResult> OnPostToggleRoleAsync(string userId, string role)
    {
        UserId = userId;
        
        if (!IsSystemAdmin) return Forbid();
        if (role != SystemRoles.Admin && role != SystemRoles.SystemAdmin) return BadRequest();

        var user = await _users.GetUserById(UserId);
        if (user == null) return NotFound();

        await _users.SetRole(UserId, role, !user.Roles.Contains(role));
        return RedirectToPage(new { userId = UserId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string userId)
    {
        UserId = userId;
        
        if (!IsSystemAdmin) return Forbid();

        if (!await _users.DeleteUser(UserId))
        {
            TempData["Error"] = "Couldn't delete this user — a SystemAdmin must have their role removed first, and you can't delete your own account.";
            return RedirectToPage(new { userId = UserId });
        }

        TempData["Success"] = "User deleted.";
        return RedirectToPage("Index");
    }
}
