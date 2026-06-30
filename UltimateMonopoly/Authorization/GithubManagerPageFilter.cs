using JC.Identity.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Authorization;

/// <summary>Funnels users who hold ONLY the GithubManager role (no Admin / SystemAdmin) to the Reported Issues
/// area — the only admin pages they may use. Every other admin page (the Dashboard included) redirects to the
/// issues list. Any page under <c>/Logs/Issues/</c> is allowed (the list and the per-issue Contact page), so
/// new issues sub-pages don't need to be allow-listed here. Dual-role admins are unaffected. Registered on the
/// Admin area via an application-model convention.</summary>
public class GithubManagerPageFilter : IAsyncPageFilter
{
    private const string IssuesArea = "/Logs/Issues/";

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var onlyGithubManager = user.IsInRole(AppRoles.GithubManager)
            && !user.IsInRole(SystemRoles.Admin)
            && !user.IsInRole(SystemRoles.SystemAdmin);

        var page = context.RouteData.Values["page"]?.ToString();
        var inIssuesArea = page != null && page.StartsWith(IssuesArea, StringComparison.OrdinalIgnoreCase);

        if (onlyGithubManager && !inIssuesArea)
        {
            context.Result = new RedirectToPageResult("/Logs/Issues/Index", new { area = "Admin" });
            return;
        }

        await next();
    }
}
