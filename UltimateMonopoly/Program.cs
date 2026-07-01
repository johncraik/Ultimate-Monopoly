using Hangfire;
using JC.Communication.Email.Models;
using JC.Communication.Email.Models.Options;
using JC.Communication.Extensions;
using JC.Communication.Notifications.Models.Options;
using JC.Core.Extensions;
using JC.Github.Extensions;
using JC.Identity.Authentication;
using JC.Identity.Extensions;
using JC.MySql;
using JC.SqlServer.Hangfire;
using JC.Web.Extensions;
using JC.Web.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using JC.Web.Security.Models;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Abstractions.Cards;
using UltimateMonopoly.Areas.Admin.Middleware;
using UltimateMonopoly.Areas.Admin.Services;
using UltimateMonopoly.Authorization;
using UltimateMonopoly.Data;
using UltimateMonopoly.Extensions;
using UltimateMonopoly.Hubs;
using UltimateMonopoly.Services;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Imports;

var builder = WebApplication.CreateBuilder(args);

// Syncfusion license
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
    builder.Configuration["SYNCFUSION_KEY"]);

// Razor Pages + API controllers
builder.Services.AddRazorPages(options =>
{
    // Gate the whole Admin area to the AdminArea policy (Admin or SystemAdmin or GithubManager) — see below.
    options.Conventions.AuthorizeAreaFolder("Admin", "/", "AdminArea");
    // GithubManager-only users may use just the Reported Issues page; this filter funnels them there from any
    // other admin page (Dashboard included). Dual-role admins are unaffected.
    options.Conventions.AddAreaFolderApplicationModelConvention("Admin", "/",
        model => model.Filters.Add(new GithubManagerPageFilter()));
    // Signed-in users have no business on the login/register flow — bounce them to account management.
    // The filter skips Manage/*, Logout, ConfirmEmail/Change, AccessDenied and Disabled (still authed-usable).
    options.Conventions.AddAreaFolderApplicationModelConvention("Identity", "/Account",
        model => model.Filters.Add(new RedirectAuthenticatedFilter()));
});
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

// JC.Core background-job retention (the jobs are registered with Hangfire in ServiceRegistration).
// Audit-entry cleanup keeps 6 months (with the package's 30-record-per-table floor). The generic
// soft-delete cleanup is deliberately left OFF: it auto-discovers EVERY soft-deletable entity and
// hard-deletes app-wide, which would bypass the bespoke game-retention pipeline (GameCleanupJob keeps
// Game/GamePlayer shells alive for the PlayerGameStat FK). Only enable it with the game-history tables
// blacklisted.
builder.Services.ConfigureCoreBackgroundJobs(options =>
{
    options.EnableAuditCleanupJob = true;
    options.AuditRetentionMonths = 6;
    // A single game-delete writes 200+ audit entries (snapshots + events + players + game), so the
    // AuditEntries table churns fast — raise the per-run delete cap above the default 500 so the
    // cleanup keeps pace with the write volume rather than falling permanently behind.
    options.AuditCleanupChunkingValue = 2000;

    options.EnableSoftDeleteCleanupJob = false;
});

// Identity. Disabled (but still signed-in) users are redirected to a dedicated page — distinct from the
// generic 403 AccessDenied — that explains the disable and offers log out / self-delete. AccessDeniedRoute
// is auto-added to the middleware's ExcludedPaths, so a disabled user can actually load it.
builder.Services.AddIdentity<AppUser, AppRole, AppDbContext>(
    configureMiddleware: options =>
    {
        options.AccessDeniedRoute = "/Identity/Account/Disabled";
    });

// Identity password policy + email confirmation. Configured after AddIdentity so these override the
// ASP.NET defaults. RequireConfirmedAccount gates sign-in on a confirmed email — the register / external-
// login flows already branch on it (no auto-login until the user confirms via the emailed link).
builder.Services.Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;   // at least one special character
    
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@";

    options.SignIn.RequireConfirmedAccount = true;     // must confirm email before signing in
    
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 5;
});

// Global authorisation — every page requires an authenticated user by default.
// Pages that must be public (Login, Register, password reset, etc.) opt out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Admin area — Admin or SystemAdmin (read + moderation). SystemAdmin-only pages/actions take the
    // SystemAdminOnly policy on top. See design-docs/c1-admin-area.md §4.
    options.AddPolicy("AdminArea", p => p.RequireRole(SystemRoles.Admin, SystemRoles.SystemAdmin, AppRoles.GithubManager));
    options.AddPolicy("SystemAdminOnly", p => p.RequireRole(SystemRoles.SystemAdmin));
});

// Web (security headers, cookies, client profiling). TrustProxyHeaders so the real client IP is
// resolved from Cloudflare's CF-Connecting-IP (the app runs behind a Cloudflare tunnel on IIS) —
// this drives the per-IP rate limiter below. Bot filtering is OFF: public pages (home, guides,
// rules) should be crawlable for SEO, and authed pages are already gated, so blocking detected
// bots adds nothing here — and Cloudflare handles bot management in front anyway.
builder.Services.AddWebDefaults(builder.Configuration,
    configureBotFilter: bots => bots.IsEnabled = false,
    configureClientIp: ip => ip.TrustProxyHeaders = true);

// Rate limiting (opt-in) — scoped to the Identity auth endpoints via UseWhen in the pipeline below.
// Per-IP brute-force backstop on login / register / password / 2FA. Token bucket (not fixed-window) so a
// legitimate fumbling session bursts through — wrong-password retries, register-then-login, confirm-email,
// 2FA, plus a second user behind the same NAT/CGNAT IP — while the sustained refill still throttles an
// automated hammer to a crawl. The real brute-force defence is per-account lockout; this is the coarse
// per-IP cap on top. Burst up to 30 (TokenLimit); sustained 15/min (TokensPerPeriod over the 1-min Window).
// PermitLimit is intentionally unset — it is a no-op under TokenBucket (TokenLimit drives capacity).
builder.Services.AddRateLimiting(options =>
{
    options.Strategy = RateLimitingStrategy.TokenBucket;
    options.TokenLimit = 30;
    options.TokensPerPeriod = 15;
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
        ? EmailProvider.Microsoft 
        : EmailProvider.Microsoft;
    options.LoggingMode = builder.Environment.IsDevelopment()
        ? EmailLoggingMode.FullLog
        : EmailLoggingMode.ExcludeContent;
});
// Email-log retention — keep 6 months (registered with Hangfire in ServiceRegistration).
builder.Services.ConfigureEmailBackgroundJobs(options =>
{
    options.EnableEmailLogCleanupJob = true;
    options.EmailLogRetentionMonths = 6;
});

// Communication — Messaging (E1 — friends-only DMs; no group chats)
builder.Services.AddMessaging<AppDbContext>(o => o.DisableGroups = true);
// Messaging-log retention — activity + read logs, keep 6 months.
builder.Services.ConfigureMessagingBackgroundJobs(options =>
{
    options.EnableActivityLogCleanupJob = true;
    options.ActivityLogRetentionMonths = 6;

    options.EnableReadLogCleanupJob = true;
    options.ReadLogRetentionMonths = 6;
});

// Communication — Notifications
builder.Services.AddNotifications<AppDbContext>();
// Notification-log retention — keep 6 months. (No dedicated Configure extension ships for this one, so
// the options are set through the standard Options pipeline the job reads via IOptions<>.)
builder.Services.Configure<NotificationBackgroundJobOptions>(options =>
{
    options.EnableNotificationLogCleanupJob = true;
    options.NotificationLogRetentionMonths = 6;
});

//Background Jobs — Hangfire
builder.Services.AddHangfireSqlServer(builder.Configuration);

builder.Services.AddServices();
builder.Services.AddAdminServices();

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

// Propagate admin-driven role/account changes to the affected user's own live session — runs after
// UseIdentity so IUserInfo is populated and the principal is authenticated (design-docs/c1-admin-area.md §6.2).
app.UseMiddleware<AuthRefreshMiddleware>();

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

// XML sitemap — only the publicly crawlable content pages (everything else is behind global auth).
// Built from the live request host so the <loc>s are correct across dev / staging / prod without a
// hardcoded domain. AllowAnonymous so the global "require authenticated user" fallback doesn't gate it.
app.MapGet("/sitemap.xml", (HttpContext ctx) =>
{
    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    string[] paths = ["/", "/Rules", "/Guides"];

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
    foreach (var path in paths)
        sb.AppendLine($"  <url><loc>{baseUrl}{path}</loc></url>");
    sb.AppendLine("</urlset>");

    return Results.Content(sb.ToString(), "application/xml");
}).AllowAnonymous();

// Short routes for Identity pages
app.MapGet("/login", () => Results.Redirect("/Identity/Account/Login"));
app.MapGet("/register", () => Results.Redirect("/Identity/Account/Register"));
app.MapGet("/account", () => Results.Redirect("/Identity/Account/Manage"));
app.MapGet("/profile", () => Results.Redirect("/Identity/Profile"));
app.MapGet("/social", () => Results.Redirect("/Social/Friends"));
app.MapGet("/friends", () => Results.Redirect("/Social/Friends"));
app.MapGet("/chat", () => Results.Redirect("/Social/Messages/Index"));
app.MapGet("/message", () => Results.Redirect("/Social/Messages/Index"));
app.MapGet("/join", () => Results.Redirect("/Games/Join"));

app.MapRazorPages();
app.MapControllers();
app.MapHub<PresenceHub>("/hubs/presence");
app.MapHub<GameSetupHub>("/hubs/game-setup");
app.MapHub<GamePlayHub>("/hubs/game-play");
app.MapHub<MessagingHub>("/hubs/messaging");

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
    var cardCache = scope.ServiceProvider.GetRequiredService<ICardCacheService>();
    var blockedWordImport = scope.ServiceProvider.GetRequiredService<BlockedWordImportService>();

    await boardCache.GetDefaultBoard();
    await cardCache.GetCards();
    await blockedWordImport.SeedFromFileAsync();
    
    var taxService = scope.ServiceProvider.GetRequiredService<ITurnTaxService>();
    await taxService.Import();

    var settings = scope.ServiceProvider.GetRequiredService<SettingsDictionary>();
    await settings.Import();
}
