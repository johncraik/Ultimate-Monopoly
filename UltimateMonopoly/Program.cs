using JC.Communication.Extensions;
using JC.Core.Extensions;
using JC.Github.Extensions;
using JC.Identity.Extensions;
using JC.MySql;
using JC.SqlServer.Hangfire;
using JC.Web.Extensions;
using UltimateMonopoly.Data;
using UltimateMonopoly.Extensions;
using UltimateMonopoly.Services.GameConfig;
using UltimateMonopoly.Services.Imports;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages
builder.Services.AddRazorPages();

// Database
builder.Services.AddMySqlDatabase<AppDbContext>(builder.Configuration, migrationsAssembly: "UltimateMonopoly");

// Core
builder.Services.AddCore<AppDbContext>();

// Identity
builder.Services.AddIdentity<AppUser, AppRole, AppDbContext>();

// Web (security headers, cookies, client profiling)
builder.Services.AddWebDefaults(builder.Configuration);

// Github
builder.Services.AddGithub<AppDbContext>(builder.Configuration);

// Communication — Email
builder.Services.AddEmail<AppDbContext>(builder.Configuration);

// Communication — Messaging
builder.Services.AddMessaging<AppDbContext>();

// Communication — Notifications
builder.Services.AddNotifications<AppDbContext>();

// Background Jobs — Hangfire
builder.Services.AddHangfireSqlServer(builder.Configuration, configureSqlStorage: opts =>
{
    //TODO Remove:
    opts.SqlClientFactory = Microsoft.Data.SqlClient.SqlClientFactory.Instance;
});

builder.Services.AddServices();

var app = builder.Build();

// Middleware
app.UseStaticFiles();
app.UseIdentity();
app.UseWebDefaults();
app.UseGithubWebhooks();

// Auto-migrate
await app.Services.MigrateDatabaseAsync<AppDbContext>();

// Seed admin and roles
await app.ConfigureAdminAndRolesAsync<AppUser, AppRole, AppDbContext, AppRoles>();

// Short routes for Identity pages
app.MapGet("/login", () => Results.Redirect("/Identity/Account/Login"));
app.MapGet("/register", () => Results.Redirect("/Identity/Account/Register"));

app.MapRazorPages();

//Create defaults:
await SetupDefaults();

app.Run();


async Task SetupDefaults()
{
    await using var scope = app.Services.CreateAsyncScope();
    var boardCache = scope.ServiceProvider.GetRequiredService<BoardCacheService>();
    
    await boardCache.GetDefaultBoard();
}
