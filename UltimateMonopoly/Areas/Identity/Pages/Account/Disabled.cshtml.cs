using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Areas.Identity.Pages.Account;

/// <summary>
/// Shown to a signed-in user whose account has been disabled by an admin (the identity middleware's
/// AccessDeniedRoute). They can't reach anything else, so the only actions are to log out or to
/// permanently delete their own account. All logic lives here — no service layer needed.
/// </summary>
public class DisabledModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;

    public DisabledModel(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
            await _userManager.DeleteAsync(user);

        await _signInManager.SignOutAsync();
        return RedirectToPage("/Index");
    }
}
