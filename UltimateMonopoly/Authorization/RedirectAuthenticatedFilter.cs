using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UltimateMonopoly.Authorization;

/// <summary>Sends already-signed-in users away from the pre-authentication pages (login, register and their
/// sub-flows — 2FA, recovery codes, external login, resend / register confirmation, forgot / reset password)
/// to account management, so a logged-in user can't sit on a login/register form. Pages an authenticated user
/// legitimately uses are skipped: anything under <c>/Account/Manage/</c>, plus Logout, ConfirmEmail,
/// ConfirmEmailChange, AccessDenied and Disabled. Registered on the Identity area's <c>/Account</c> folder via
/// an application-model convention. Only signed-in visitors are affected; anonymous users see the pages normally.</summary>
public class RedirectAuthenticatedFilter : IAsyncPageFilter
{
    // Pages a signed-in user may still need under /Account — never redirect these.
    private static readonly string[] AllowedWhileAuthed =
    {
        "/Account/Logout",
        "/Account/ConfirmEmail",
        "/Account/ConfirmEmailChange",
        "/Account/AccessDenied",
        "/Account/Disabled",
    };

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var page = context.RouteData.Values["page"]?.ToString() ?? "";

            var allowed = page.StartsWith("/Account/Manage", StringComparison.OrdinalIgnoreCase)
                || AllowedWhileAuthed.Contains(page, StringComparer.OrdinalIgnoreCase);

            if (!allowed)
            {
                context.Result = new RedirectToPageResult("/Account/Manage/Index", new { area = "Identity" });
                return;
            }
        }

        await next();
    }
}
