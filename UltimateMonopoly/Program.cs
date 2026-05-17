using JC.Communication.Extensions;
using JC.Core.Extensions;
using JC.Github.Extensions;
using JC.Identity.Extensions;
using JC.MySql;
using JC.SqlServer.Hangfire;
using JC.Web.Extensions;
using UltimateMonopoly.Data;

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
builder.Services.AddHangfireSqlServer(builder.Configuration);

var app = builder.Build();

// Middleware
app.UseStaticFiles();
app.UseIdentity();
app.UseWebDefaults();
app.UseGithubWebhooks();

// Auto-migrate in development
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>();
}

// Seed admin and roles
await app.ConfigureAdminAndRolesAsync<AppUser, AppRole, AppDbContext, AppRoles>();

app.MapRazorPages();

app.Run();
