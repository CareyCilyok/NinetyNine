namespace NinetyNine.Web.Auth.EmailSender;

/// <summary>
/// Development-mode <see cref="IEmailSender"/> that writes email details to the
/// structured application logger instead of performing any SMTP delivery.
/// Register this implementation when <c>Email:Provider</c> is <c>"Console"</c>
/// or when no SMTP configuration is present.
/// </summary>
/// <remarks>
/// Never performs any network I/O. Safe to use in local development and CI
/// pipelines without an SMTP relay.
/// </remarks>
public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    /// <summary>
    /// Initialises a new <see cref="ConsoleEmailSender"/>.
    /// </summary>
    /// <param name="logger">Structured logger for recording email details.</param>
    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Logs the recipient, display name, and verification URL at
    /// <see cref="LogLevel.Information"/> — no SMTP call is made.
    /// </remarks>
    public Task SendVerificationAsync(
        string toEmail,
        string displayName,
        string verifyUrl,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[EMAIL:Verification] to={ToEmail} name={DisplayName} verifyUrl={VerifyUrl}",
            toEmail,
            displayName,
            verifyUrl);

        _logger.LogInformation(
            "========================================\n" +
            "NinetyNine email [VERIFICATION]\n" +
            "To: {ToEmail}\n" +
            "Link: {VerifyUrl}\n" +
            "========================================",
            toEmail,
            verifyUrl);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Logs the recipient, display name, and password-reset URL at
    /// <see cref="LogLevel.Information"/> — no SMTP call is made.
    /// </remarks>
    public Task SendPasswordResetAsync(
        string toEmail,
        string displayName,
        string resetUrl,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[EMAIL:PasswordReset] to={ToEmail} name={DisplayName} resetUrl={ResetUrl}",
            toEmail,
            displayName,
            resetUrl);

        _logger.LogInformation(
            "========================================\n" +
            "NinetyNine email [PASSWORD RESET]\n" +
            "To: {ToEmail}\n" +
            "Link: {ResetUrl}\n" +
            "========================================",
            toEmail,
            resetUrl);

        return Task.CompletedTask;
    }
}
