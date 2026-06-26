using JC.Identity.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Authorization;

/// <summary>Funnels users who hold ONLY the GithubManager role (no Admin / SystemAdmin) to the Reported Issues
/// page — the single admin page they may use. Every other admin page (the Dashboard included) redirects there.
/// Dual-role admins are unaffected. Registered on the Admin area via an application-model convention.</summary>
public class GithubManagerPageFilter : IAsyncPageFilter
{
    private const string IssuesPage = "/Logs/Issues/Index";

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var onlyGithubManager = user.IsInRole(AppRoles.GithubManager)
            && !user.IsInRole(SystemRoles.Admin)
            && !user.IsInRole(SystemRoles.SystemAdmin);

        if (onlyGithubManager &&
            !string.Equals(context.RouteData.Values["page"]?.ToString(), IssuesPage, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new RedirectToPageResult("/Logs/Issues/Index", new { area = "Admin" });
            return;
        }

        await next();
    }
}
