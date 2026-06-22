#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using JC.Communication.Email.Models;
using JC.Communication.Email.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using UltimateMonopoly.Data;
using UltimateMonopoly.Services;

namespace UltimateMonopoly.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IEmailService _emailService;
    private readonly ProfanityService _profanityService;
    private readonly ProfileService _profileService;

    public IndexModel(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IEmailService emailService,
        ProfanityService profanityService,
        ProfileService profileService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
        _profanityService = profanityService;
        _profileService = profileService;
    }

    public string Username { get; set; }
    public string Email { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public bool IsHidden { get; set; }

    [TempData] public string StatusMessage { get; set; }

    [BindProperty] public ProfileInputModel Input { get; set; }
    [BindProperty] public EmailInputModel EmailInput { get; set; }

    public class ProfileInputModel
    {
        [StringLength(64, ErrorMessage = "Display name must be 64 characters or fewer.")]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; }

        [Phone]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; }
    }

    public class EmailInputModel
    {
        // Not [Required] on purpose: this lives on the same page as the profile form, so an empty value
        // when the *other* form posts must not invalidate it. Required-ness is enforced in the handler.
        [EmailAddress]
        [Display(Name = "New email")]
        public string NewEmail { get; set; }
    }

    private async Task LoadAsync(AppUser user)
    {
        Username = await _userManager.GetUserNameAsync(user);
        Email = await _userManager.GetEmailAsync(user);
        IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        IsHidden = await _userManager.IsInRoleAsync(user, AppRoles.HiddenUser);

        Input = new ProfileInputModel
        {
            DisplayName = user.DisplayName,
            PhoneNumber = await _userManager.GetPhoneNumberAsync(user)
        };
        EmailInput = new EmailInputModel { NewEmail = Email };
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostProfileAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

        // Profanity gate on the display name (B1) — generic message; the matched term goes to the audit/log,
        // never the user-facing string.
        if (!string.IsNullOrWhiteSpace(Input.DisplayName))
        {
            var profanity = await _profanityService.Check(Input.DisplayName);
            if (profanity.IsProfane)
                ModelState.AddModelError("Input.DisplayName", "This display name isn't allowed. Please choose another.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        // Display name — collisions allowed (not unique); blank clears it back to the username fallback.
        var newDisplayName = string.IsNullOrWhiteSpace(Input.DisplayName) ? null : Input.DisplayName.Trim();
        if (user.DisplayName != newDisplayName)
        {
            user.DisplayName = newDisplayName;
            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                StatusMessage = "Error: could not update your display name.";
                return RedirectToPage();
            }
        }

        var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        if (Input.PhoneNumber != phoneNumber)
        {
            var setPhone = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            if (!setPhone.Succeeded)
            {
                StatusMessage = "Error: could not update your phone number.";
                return RedirectToPage();
            }
        }

        await _signInManager.RefreshSignInAsync(user); // refresh the display_name claim immediately
        StatusMessage = "Your account details have been updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangeEmailAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

        if (string.IsNullOrWhiteSpace(EmailInput.NewEmail) || !ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        var email = await _userManager.GetEmailAsync(user);
        if (EmailInput.NewEmail != email)
        {
            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateChangeEmailTokenAsync(user, EmailInput.NewEmail);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmailChange",
                pageHandler: null,
                values: new { area = "Identity", userId, email = EmailInput.NewEmail, code },
                protocol: Request.Scheme);

            var result = await _emailService.SendAsync(
                new[] { new EmailRecipient(EmailInput.NewEmail) },
                "Confirm your email",
                plainBody: $"Please confirm your account by visiting: {callbackUrl}",
                htmlBody: $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            StatusMessage = result.Succeeded
                ? "Confirmation link to change email sent. Please check your email."
                : $"Error: failed to send confirmation email: {result.ErrorMessage}";
            return RedirectToPage();
        }

        StatusMessage = "Your email is unchanged.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendVerificationEmailAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

        var userId = await _userManager.GetUserIdAsync(user);
        var email = await _userManager.GetEmailAsync(user);
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { area = "Identity", userId, code },
            protocol: Request.Scheme);

        var result = await _emailService.SendAsync(
            new[] { new EmailRecipient(email) },
            "Confirm your email",
            plainBody: $"Please confirm your account by visiting: {callbackUrl}",
            htmlBody: $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

        StatusMessage = result.Succeeded
            ? "Verification email sent. Please check your email."
            : $"Error: failed to send verification email: {result.ErrorMessage}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostHideAsync()
    {
        var ok = await _profileService.TryHideUser();
        if (ok)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) await _signInManager.RefreshSignInAsync(user);
        }

        StatusMessage = ok
            ? "You are now hidden from the public leaderboard."
            : "Error: could not update your visibility.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostShowAsync()
    {
        var ok = await _profileService.TryUnhideUser();
        if (ok)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) await _signInManager.RefreshSignInAsync(user);
        }

        StatusMessage = ok
            ? "You are now visible on the public leaderboard."
            : "Error: could not update your visibility.";
        return RedirectToPage();
    }
}