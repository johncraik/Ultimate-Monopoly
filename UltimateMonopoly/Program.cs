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
using JC.Web.RateLimiting;
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

// SignalR. Explicit keep-alive / timeout so the server↔client heartbeat is documented and the
// 2:1 ClientTimeout:KeepAlive ratio (the SignalR-recommended floor) is guaranteed: the server
// pings every 15s and considers a client gone after 30s of silence. Detailed errors only in dev.
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

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

// Web (security headers, cookies, client profiling). TrustProxyHeaders so the real client IP is
// resolved from Cloudflare's CF-Connecting-IP (the app runs behind a Cloudflare tunnel on IIS) —
// this drives the per-IP rate limiter below (and bot filtering / geo).
builder.Services.AddWebDefaults(builder.Configuration,
    configureClientIp: ip => ip.TrustProxyHeaders = true);

// Rate limiting (opt-in) — scoped to the Identity auth endpoints via UseWhen in the pipeline below.
// Per-IP brute-force defence on login / register / password / 2FA: 10 requests per minute per client IP.
builder.Services.AddRateLimiting(options =>
{
    options.Strategy = RateLimitingStrategy.TokenBucket;
    options.PermitLimit = 20;
    options.Window = TimeSpan.FromMinutes(1);
    options.PartitionBy = RateLimitPartitionBy.ClientIp;
});

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

// Error handling — must wrap the whole pipeline, so it goes first. Unhandled exceptions re-execute to
// the friendly /Error page (a developer page in dev for debugging); status responses (403/404/429/…)
// re-execute to /Error/{code}. The /Error page is [AllowAnonymous] and lives outside /Identity/Account,
// so it's never blocked by global auth nor caught by the auth-endpoint rate limiter.
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/Error");

app.UseStatusCodePagesWithReExecute("/Error/{0}");

// Middleware
app.UseStaticFiles();
app.UseIdentity();
app.UseWebDefaults();

// Rate limit ONLY the Identity auth endpoints (login / register / forgot-password / reset / 2FA, …),
// not account management (/Identity/Account/Manage) or the profile. Global limiter scoped via UseWhen.
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/Identity/Account")
           && !ctx.Request.Path.StartsWithSegments("/Identity/Account/Manage"),
    branch => branch.UseRateLimiting());

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
