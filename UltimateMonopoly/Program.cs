using Hangfire;
using JC.Communication.Email.Models;
using JC.Communication.Email.Models.Options;
using JC.Communication.Extensions;
using JC.Core.Extensions;
using JC.Github.Extensions;
using JC.Identity.Extensions;
using JC.MySql;
using JC.SqlServer.Hangfire;
using JC.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using JC.Web.Security.Models;
using UltimateMonopoly.Authorization;
using UltimateMonopoly.Data;
using UltimateMonopoly.Extensions;
using UltimateMonopoly.Hubs;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Cache;

var builder = WebApplication.CreateBuilder(args);

// Syncfusion license
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
    builder.Configuration["SYNCFUSION_KEY"]);

// Razor Pages + API controllers
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Antiforgery — expose a header so AJAX calls can send the token
builder.Services.AddAntiforgery(opts => opts.HeaderName = "RequestVerificationToken");

// SignalR
builder.Services.AddSignalR();

// Database
builder.Services.AddMySqlDatabase<AppDbContext>(builder.Configuration, migrationsAssembly: "UltimateMonopoly");

// Core
builder.Services.AddCore<AppDbContext>();

// Identity
builder.Services.AddIdentity<AppUser, AppRole, AppDbContext>();

// Global authorisation — every page requires an authenticated user by default.
// Pages that must be public (Login, Register, password reset, etc.) opt out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Web (security headers, cookies, client profiling)
builder.Services.AddWebDefaults(builder.Configuration);

// Github
builder.Services.AddGithub<AppDbContext>(builder.Configuration, options =>
{
    options.GithubRepoOwner = "johncraik";
    options.GithubRepoName = "Ultimate-Monopoly";
});

// Communication — Email
builder.Services.AddEmail<AppDbContext>(builder.Configuration, options =>
{
    options.Provider = builder.Environment.IsDevelopment() 
        ? EmailProvider.Console 
        : EmailProvider.Microsoft;
    options.LoggingMode = builder.Environment.IsDevelopment() 
        ? EmailLoggingMode.FullLog
        : EmailLoggingMode.ExcludeContent;
});

// Communication — Messaging
builder.Services.AddMessaging<AppDbContext>();

// Communication — Notifications
builder.Services.AddNotifications<AppDbContext>();

//Background Jobs — Hangfire
builder.Services.AddHangfireSqlServer(builder.Configuration);

builder.Services.AddServices();

var app = builder.Build();

// Middleware
app.UseStaticFiles();
app.UseIdentity();
app.UseWebDefaults();
app.UseGithubWebhooks();

//Hangfire dashboard — SystemAdmin only
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "Ultimate Monopoly — Background Jobs",
    Authorization = [new HangfireDashboardAuthFilter()]
});

// Auto-migrate
await app.Services.MigrateDatabaseAsync<AppDbContext>();

// Seed admin and roles
await app.ConfigureAdminAndRolesAsync<AppUser, AppRole, AppDbContext, AppRoles>();

// Short routes for Identity pages
app.MapGet("/login", () => Results.Redirect("/Identity/Account/Login"));
app.MapGet("/register", () => Results.Redirect("/Identity/Account/Register"));
app.MapGet("/account", () => Results.Redirect("/Identity/Account/Manage"));
app.MapGet("/profile", () => Results.Redirect("/Identity/Profile"));
app.MapGet("/social", () => Results.Redirect("/Social/Friends"));
app.MapGet("/friends", () => Results.Redirect("/Social/Friends"));
app.MapGet("/join", () => Results.Redirect("/Games/Join"));

app.MapRazorPages();
app.MapControllers();
app.MapHub<PresenceHub>("/hubs/presence");
app.MapHub<GameSetupHub>("/hubs/game-setup");
app.MapHub<GamePlayHub>("/hubs/game-play");

// Profile cookie — 90-day encrypted cookie holding the user's avatar choices
app.PopulateEncryptedCookieProfiles(
    (ProfileService.CookieName, ProfileService.ProtectorPurpose,
        new CookieDefaultOverride(
            sameSite: SameSiteMode.Lax,
            httpOnly: true,
            maxAge: TimeSpan.FromDays(90))));

//Create defaults:
await SetupDefaults();

app.Run();


async Task SetupDefaults()
{
    await using var scope = app.Services.CreateAsyncScope();
    var boardCache = scope.ServiceProvider.GetRequiredService<BoardCacheService>();
    
    await boardCache.GetDefaultBoard();
}
