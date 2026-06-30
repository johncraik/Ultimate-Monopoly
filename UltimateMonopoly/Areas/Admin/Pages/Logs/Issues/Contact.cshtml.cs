using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Logs;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Logs.Issues;

/// <summary>Contact the local reporter of an issue (C1 — Reported Issues). GET shows the issue context + a
/// compose form; POST emails the reporter via <see cref="IssueContactService"/>. Reachable by GithubManager
/// (the Issues area is their remit) as well as Admin / SystemAdmin.</summary>
public class ContactModel : PageModel
{
    private readonly IssueContactService _contact;

    public ContactModel(IssueContactService contact) => _contact = contact;

    [BindProperty(SupportsGet = true)]
    public string IssueId { get; set; } = "";

    public IssueContactViewModel Issue { get; private set; } = default!;

    [BindProperty]
    public string Subject { get; set; } = "";

    [BindProperty]
    public string Message { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var ctx = await _contact.GetContext(IssueId);
        if (ctx == null) return NotFound();

        Issue = ctx;
        Subject = ctx.DefaultSubject;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var ctx = await _contact.GetContext(IssueId);
        if (ctx == null) return NotFound();
        Issue = ctx;

        // The reporter became uncontactable since the form loaded (deleted account, etc.) — terminal.
        if (!ctx.CanContact)
        {
            TempData["Error"] = ctx.BlockReason ?? "This reporter can't be contacted.";
            return RedirectToPage(new { issueId = IssueId });
        }

        // Empty message / send failure keep the typed text on the page (return Page, don't redirect).
        if (string.IsNullOrWhiteSpace(Message))
        {
            ModelState.AddModelError(nameof(Message), "Please write a message to send.");
            return Page();
        }

        var result = await _contact.SendContact(IssueId, Subject, Message);
        if (result == IssueContactResult.Sent)
        {
            TempData["Success"] = $"Your message has been sent to {ctx.ReporterName}.";
            return RedirectToPage(new { issueId = IssueId });
        }

        if (result == IssueContactResult.SendFailed)
        {
            ModelState.AddModelError(string.Empty, "The email could not be sent. Please try again.");
            return Page();
        }

        // Terminal reporter/issue states — re-checked server-side; surface the reason and reset.
        TempData["Error"] = result switch
        {
            IssueContactResult.NoEmail => "The reporter has no email address on file.",
            IssueContactResult.ReporterMissing => "The reporter's account no longer exists.",
            IssueContactResult.NoReporter => "This issue has no registered reporter to contact.",
            _ => "That issue no longer exists."
        };
        return RedirectToPage(new { issueId = IssueId });
    }
}