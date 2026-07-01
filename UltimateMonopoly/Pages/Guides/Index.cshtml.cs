using System.ComponentModel.DataAnnotations;
using JC.Communication.Email.Helpers;
using JC.Communication.Email.Models;
using JC.Communication.Email.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MP.GameEngine.Helpers.RuleSet;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Pages.Guides;

// Public — linked from the signed-out navbar's "How To Play". The Quick Start walkthrough and the
// "guides coming soon" placeholder are public; the Contact tab + form are authenticated-only (rendered
// only when signed in, and OnPostContact re-checks). The guides content backend (Guide table, file area,
// admin upload/replace) is deferred to V2.
[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly UserManager<AppUser> _userManager;
    private readonly DefaultEmailBranding _branding;

    public IndexModel(IEmailService email, IConfiguration config, UserManager<AppUser> userManager, DefaultEmailBranding branding)
    {
        _email = email;
        _config = config;
        _userManager = userManager;
        _branding = branding;
    }

    /// <summary>Which tab renders active — "quickstart" by default; "contact" after a contact post / status.</summary>
    public string ActiveTab { get; private set; } = "quickstart";

    [BindProperty]
    public ContactForm Contact { get; set; } = new();

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? StatusKind { get; set; }

    public async Task OnGetAsync(string? tab = null)
    {
        if (StatusMessage is not null)
            ActiveTab = "contact";                      // returned from a send → show the result
        else if (tab is "quickstart" or "guides" or "contact")
            ActiveTab = tab;                            // deep link (e.g. the footer's "Contact")

        var isAuth = User.Identity?.IsAuthenticated == true;

        // Contact is auth-only — don't open it for anonymous visitors.
        if (ActiveTab == "contact" && !isAuth)
            ActiveTab = "quickstart";

        // Pre-fill the contact email with the signed-in user's address (the form is auth-only).
        if (isAuth)
        {
            var user = await _userManager.GetUserAsync(User);
            Contact.Email = user?.Email ?? "";
        }
    }

    public async Task<IActionResult> OnPostContactAsync()
    {
        // Authenticated users only — the form isn't rendered for anonymous visitors; re-check here.
        if (User.Identity?.IsAuthenticated != true)
            return Forbid();

        if (!ModelState.IsValid)
        {
            ActiveTab = "contact";
            return Page();
        }

        // Contact messages go to the support inbox (overridable via config).
        var recipient = _config["Communication:Email:ContactRecipient"] ?? "support@monappoly.com";

        var sender = await _userManager.GetUserAsync(User);
        var subject = $"[Contact] {Contact.Subject.Trim()}";

        // Same branded shell as the admin reply, via EmailBuilder — encodes the user-supplied fields for us.
        var (plain, html) = EmailBodyBuilder.Create(_branding.Get(), "New contact message")
            .Paragraph($"A visitor sent a contact message from {RuleDictionary.GameName}.")
            .Paragraph($"From: {sender?.UserName} <{Contact.Email.Trim()}>\nSubject: {Contact.Subject.Trim()}")
            .Quote(Contact.Message.Trim())
            .Footer($"Reply directly to {Contact.Email.Trim()}.")
            .Build();

        // Simple overload → uses the configured DefaultFromAddress / DisplayName as the sender (auto-logs).
        var result = await _email.SendAsync(new[] { new EmailRecipient(recipient) }, subject, plain, html);

        if (result.Succeeded)
        {
            StatusMessage = "Thanks — your message has been sent. We'll get back to you by email.";
            StatusKind = "success";
        }
        else
        {
            StatusMessage = "Sorry — we couldn't send your message just now. Please try again later.";
            StatusKind = "danger";
        }

        return RedirectToPage();
    }

    public class ContactForm
    {
        [Required(ErrorMessage = "Please enter your email so we can reply.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Your email")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Please enter a subject.")]
        [StringLength(150)]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = "";

        [Required(ErrorMessage = "Please enter a message.")]
        [StringLength(4000, MinimumLength = 1)]
        [Display(Name = "Message")]
        public string Message { get; set; } = "";
    }
}
