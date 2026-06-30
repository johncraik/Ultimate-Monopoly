namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;

/// <summary>The minimal contact details for a user — just what's needed to email them (the issue-reporter
/// contact flow), without exposing the full <see cref="UserViewModel"/> (roles / 2FA / lockout / last-login).
/// Lets a GithubManager email a reporter without full user-detail access. <see cref="DisplayName"/> is already
/// resolved (falls back to the username when the stored display name is null or empty).</summary>
public record UserContactInfo(string? Email, string DisplayName, bool EmailConfirmed);