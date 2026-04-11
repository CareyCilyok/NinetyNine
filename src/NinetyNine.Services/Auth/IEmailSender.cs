namespace NinetyNine.Services.Auth;

/// <summary>
/// Abstraction for transactional email delivery used by the authentication service.
/// Implementations live in <c>NinetyNine.Web</c>; this interface is defined in the
/// Services layer so that <see cref="AuthService"/> can depend on it without a
/// circular project reference.
/// </summary>
/// <remarks>
/// Three implementations ship with <c>NinetyNine.Web</c>:
/// <list type="bullet">
///   <item><c>MailKitEmailSender</c> — production SMTP via MailKit.</item>
///   <item><c>ConsoleEmailSender</c> — dev fallback that logs to the structured logger.</item>
///   <item><c>MockEmailSender</c> — in-memory accumulator for integration tests.</item>
/// </list>
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
    Task SendPasswordResetAsync(
        string toEmail,
        string displayName,
        string resetUrl,
        CancellationToken ct);
}
