using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UltimateMonopoly.Pages;

/// <summary>
/// The public, user-friendly error page (<c>/Error</c> and <c>/Error/{code}</c>). Reached two ways:
/// <c>UseExceptionHandler("/Error")</c> for unhandled 500s, and <c>UseStatusCodePagesWithReExecute("/Error/{0}")</c>
/// for status responses (403 / 404 / 429 / …). <see cref="AllowAnonymousAttribute"/> so it shows under the
/// global authorization policy (an error must never bounce an anonymous user to login). It lives outside
/// <c>/Identity/Account</c>, so the auth-endpoint rate limiter never applies to it (no re-execute loop on 429).
/// </summary>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    /// <summary>The HTTP status code being shown (defaults to 500 for an unhandled error).</summary>
    public int StatusCode { get; private set; } = 500;

    /// <summary>Short, friendly heading for the status.</summary>
    public string Title { get; private set; } = "Something went wrong";

    /// <summary>Reassuring, non-technical explanation.</summary>
    public string Message { get; private set; } = "An unexpected error occurred. Please try again in a moment.";

    /// <summary>Bootstrap icon class for the status.</summary>
    public string Icon { get; private set; } = "bi-exclamation-octagon";

    public void OnGet(int? code)
    {
        // The re-execute path supplies the original status in the route; the exception-handler path (no
        // route value) falls back to the response status the pipeline already set (500 for unhandled errors).
        StatusCode = code ?? Response.StatusCode;
        if (StatusCode < 400)
            StatusCode = 500;

        (Title, Message, Icon) = StatusCode switch
        {
            400 => ("Bad request", "We couldn't understand that request. Check the address and try again.", "bi-exclamation-diamond"),
            403 => ("Access denied", "You don't have permission to view this page.", "bi-shield-lock"),
            404 => ("Page not found", "We couldn't find the page you were looking for — it may have moved or no longer exists.", "bi-compass"),
            429 => ("Slow down a moment", "You've made too many requests in a short time. Please wait a minute, then try again.", "bi-hourglass-split"),
            >= 500 => ("Something went wrong", "An unexpected error occurred on our end. Please try again in a moment.", "bi-exclamation-octagon"),
            _ => ("Something went wrong", "An unexpected error occurred. Please try again.", "bi-exclamation-octagon")
        };

        // Preserve the real status code on the rendered response (so clients / proxies still see 404/500/etc.).
        Response.StatusCode = StatusCode;
    }
}