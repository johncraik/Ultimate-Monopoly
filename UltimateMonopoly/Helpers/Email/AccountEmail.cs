namespace UltimateMonopoly.Helpers.Email;

/// <summary>
/// Ready-made plain + HTML bodies for the ASP.NET Identity account emails, composed via <see cref="EmailBuilder"/>
/// so every confirmation / reset mail shares the branded shell and reads consistently. Each method takes the
/// Identity-generated callback URL and returns the two bodies for <c>IEmailService.SendAsync</c>. Kept here (rather
/// than inline in each page model) so the four "confirm your account" flows can't drift apart.
/// </summary>
public static class AccountEmail
{
    /// <summary>Account email-confirmation link (registration, resend, external login, re-verify).</summary>
    public static (string Plain, string Html) ConfirmAccount(string callbackUrl) =>
        EmailBuilder.Create("Confirm your account")
            .Paragraph("Welcome! Please confirm your email address to activate your account.")
            .Button("Confirm my account", callbackUrl)
            .Footer("If you didn't create an account, you can safely ignore this email.")
            .Build();

    /// <summary>Password-reset link.</summary>
    public static (string Plain, string Html) ResetPassword(string callbackUrl) =>
        EmailBuilder.Create("Password reset")
            .Paragraph("We received a request to reset your password. Use the button below to choose a new one.")
            .Button("Reset my password", callbackUrl)
            .Footer("If you didn't request this, you can safely ignore this email — your password won't change.")
            .Build();

    /// <summary>Confirmation link sent to a user's <em>new</em> address when they change their email.</summary>
    public static (string Plain, string Html) ConfirmEmailChange(string callbackUrl) =>
        EmailBuilder.Create("Confirm your new email")
            .Paragraph("Please confirm this is your new email address to finish updating your account.")
            .Button("Confirm this email", callbackUrl)
            .Footer("If you didn't request an email change, please contact support.")
            .Build();
}