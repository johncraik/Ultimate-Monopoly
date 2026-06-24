using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.AspNetCore.Identity;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Areas.Admin.Middleware;

/// <summary>
/// Propagates an admin-driven role/account change to the affected user's own live session
/// (C1 — design-docs/c1-admin-area.md §6.2). Role gates read the auth cookie's claims, so a change an
/// admin makes wouldn't bite until that cookie next refreshes. When the user is flagged in the singleton
/// <see cref="AuthRefreshService"/>, this re-issues <em>their own</em> cookie on <em>their own</em> next
/// request via <see cref="SignInManager{TUser}.RefreshSignInAsync"/> and clears the flag. Always refreshes
/// the current principal — never a target user's session (the session-swap footgun).
/// Must run after JC.Identity's <c>UserInfoMiddleware</c> (i.e. after <c>app.UseIdentity()</c>) so the
/// principal is authenticated and <see cref="IUserInfo"/> is populated.
/// </summary>
public class AuthRefreshMiddleware
{
    private readonly RequestDelegate _next;

    public AuthRefreshMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context,
        AuthRefreshService authRefresh,
        SignInManager<AppUser> signInManager,
        IUserInfo userInfo)
    {
        var userId = userInfo.UserId;
        if (!string.IsNullOrEmpty(userId) && authRefresh.IsRefreshPending(userId))
        {
            // GetUserAsync resolves the AppUser from the current principal — we only ever refresh the
            // requester's own session, never the target's.
            var user = await signInManager.UserManager.GetUserAsync(context.User);
            if (user != null)
                await signInManager.RefreshSignInAsync(user);

            authRefresh.ConsumeRefreshSignIn(userId);
        }

        await _next(context);
    }
}