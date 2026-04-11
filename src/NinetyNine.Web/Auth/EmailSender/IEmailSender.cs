namespace NinetyNine.Web.Auth.EmailSender;

/// <summary>
/// Abstraction for application-level transactional email delivery.
/// Implementations must be safe to inject as scoped or singleton services.
/// </summary>
/// <remarks>
/// Three implementations ship with this assembly:
/// <list type="bullet">
///   <item><see cref="MailKitEmailSender"/> — production SMTP via MailKit.</item>
///   <item><see cref="ConsoleEmailSender"/> — dev fallback that writes to the structured logger.</item>
///   <item><see cref="MockEmailSender"/> — in-memory accumulator for integration tests.</item>
/// </list>
/// DI registration is performed by WP-05 (Program.cs / service-registration extension).
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Sends an account-verification email containing a one-time confirmation link.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="displayName">Recipient's chosen display name, shown in the email greeting.</param>
    /// <param name="verifyUrl">Fully-qualified URL the recipient must visit to verify their account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the email has been accepted by the transport.</returns>
    Task SendVerificationAsync(
        string toEmail,
        string displayName,
        string verifyUrl,
        CancellationToken ct);

    /// <summary>
    /// Sends a password-reset email containing a one-time reset link (expires in 1 hour).
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="displayName">Recipient's chosen display name, shown in the email greeting.</param>
    /// <param name="resetUrl">Fully-qualified URL the recipient must visit to reset their password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the email has been accepted by the transport.</returns>
    Task SendPasswordResetAsync(
        string toEmail,
        string displayName,
        string resetUrl,
        CancellationToken ct);
}
