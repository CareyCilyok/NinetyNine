using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace NinetyNine.Web.Auth.EmailSender;

/// <summary>
/// Production <see cref="IEmailSender"/> that delivers email over SMTP using the
/// MailKit library. SMTP connection parameters are supplied via
/// <see cref="EmailSettings"/> (bound from the <c>Email</c> configuration section).
/// </summary>
/// <remarks>
/// <para>
/// TLS negotiation strategy is selected from <see cref="EmailSettings"/> as follows:
/// <list type="bullet">
///   <item>Port 465 — <see cref="SecureSocketOptions.SslOnConnect"/> (implicit TLS).</item>
///   <item><see cref="EmailSettings.UseStartTls"/> == <see langword="true"/> — <see cref="SecureSocketOptions.StartTls"/>.</item>
///   <item>Otherwise — <see cref="SecureSocketOptions.None"/> (plain SMTP, e.g. port 25 internal relay).</item>
/// </list>
/// </para>
/// <para>
/// SMTP authentication is performed only when <see cref="EmailSettings.SmtpUsername"/>
/// is non-empty.
/// </para>
/// <para>
/// All user-supplied values embedded in the HTML body are encoded with
/// <see cref="WebUtility.HtmlEncode"/> to prevent HTML-injection attacks.
/// </para>
/// </remarks>
public sealed class MailKitEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<MailKitEmailSender> _logger;

    /// <summary>
    /// Initialises a new <see cref="MailKitEmailSender"/>.
    /// </summary>
    /// <param name="options">Bound <see cref="EmailSettings"/> from configuration.</param>
    /// <param name="logger">Structured logger for delivery diagnostics.</param>
    public MailKitEmailSender(
        IOptions<EmailSettings> options,
        ILogger<MailKitEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends an HTML email with subject <c>"Verify your NinetyNine account"</c>.
    /// The body greets the recipient by display name, explains the purpose of the link,
    /// and includes both a clickable button and the plain URL.
    /// </remarks>
    public async Task SendVerificationAsync(
        string toEmail,
        string displayName,
        string verifyUrl,
        CancellationToken ct)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        // HtmlEncode the URL for both HTML attribute context (href) and text content context.
        // Browsers decode HTML entities in href values before navigating, so a well-formed URL
        // is never broken by this encoding. It prevents attribute-injection attacks where a URL
        // containing '"' or '>' could escape the href attribute and inject HTML/event-handlers.
        var safeVerifyUrl = WebUtility.HtmlEncode(verifyUrl);
        var subject = "Verify your NinetyNine account";
        var body = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background-color:#1a1a2e;font-family:Arial,Helvetica,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#1a1a2e;padding:40px 0;">
                <tr>
                  <td align="center">
                    <table width="560" cellpadding="0" cellspacing="0" style="background-color:#16213e;border-radius:8px;padding:40px;max-width:560px;">
                      <tr>
                        <td style="color:#e0e0e0;font-size:22px;font-weight:bold;padding-bottom:24px;">
                          NinetyNine
                        </td>
                      </tr>
                      <tr>
                        <td style="color:#e0e0e0;font-size:16px;line-height:1.6;padding-bottom:16px;">
                          Hi {safeName},
                        </td>
                      </tr>
                      <tr>
                        <td style="color:#c0c0c0;font-size:15px;line-height:1.6;padding-bottom:24px;">
                          Thanks for signing up! Please confirm your email address so you can start keeping score.
                          Click the button below — the link is valid for 24 hours.
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding-bottom:24px;">
                          <a href="{safeVerifyUrl}"
                             style="display:inline-block;background-color:#0f3460;color:#e0e0e0;
                                    text-decoration:none;padding:14px 32px;border-radius:6px;
                                    font-size:15px;font-weight:bold;">
                            Verify my email
                          </a>
                        </td>
                      </tr>
                      <tr>
                        <td style="color:#909090;font-size:13px;line-height:1.5;padding-bottom:24px;word-break:break-all;">
                          If the button doesn't work, copy and paste this link into your browser:<br>
                          {safeVerifyUrl}
                        </td>
                      </tr>
                      <tr>
                        <td style="color:#707070;font-size:12px;border-top:1px solid #2a2a4a;padding-top:16px;">
                          If you didn't create a NinetyNine account, you can ignore this email.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        await SendAsync(toEmail, displayName, subject, body, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends an HTML email with subject <c>"Reset your NinetyNine password"</c>.
    /// The link expires in 1 hour. The body includes a footer advising the
    /// recipient to ignore the email if they did not request a reset.
    /// </remarks>
    public async Task SendPasswordResetAsync(
        string toEmail,
        string displayName,
        string resetUrl,
        CancellationToken ct)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        // HtmlEncode the URL for both HTML attribute context (href) and text content context.
        // Same rationale as SendVerificationAsync: prevents attribute-injection via malformed URLs.
        var safeResetUrl = WebUtility.HtmlEncode(resetUrl);
        var subject = "Reset your NinetyNine password";
        var body = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background-color:#1a1a2e;font-family:Arial,Helvetica,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#1a1a2e;padding:40px 0;">
                <tr>
                  <td align="center">
                    <table width="560" cellpadding="0" cellspacing="0" style="background-color:#16213e;border-radius:8px;padding:40px;max-width:560px;">
                      <tr>
                        <td style="color:#e0e0e0;font-size:22px;font-weight:bold;padding-bottom:24px;">
                          NinetyNine
                        </td>
                      </tr>
                      <tr>
                        <td style="color:#e0e0e0;font-size:16px;line-height:1.6;padding-bottom:16px;">
                          Hi {safeName},
                        </td>
                      </tr>
                      <tr>
                        <td style="color:#c0c0c0;font-size:15px;line-height:1.6;padding-bottom:24px;">
                          We received a request to reset the password for your NinetyNine account.
                          Click the button below to choose a new password. This link expires in 1 hour.
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding-bottom:24px;">
                          <a href="{safeResetUrl}"
                             style="display:inline-block;background-color:#0f3460;color:#e0e0e0;
                                    text-decoration:none;padding:14px 32px;border-radius:6px;
                                    font-size:15px;font-weight:bold;">
                            Reset my password
                          </a>
                        </td>
                      </tr>
                      <tr>
                        <td style="color:#909090;font-size:13px;line-height:1.5;padding-bottom:24px;word-break:break-all;">
                          If the button doesn't work, copy and paste this link into your browser:<br>
                          {safeResetUrl}
                        </td>
                      </tr>
                      <tr>
                        <td style="color:#707070;font-size:12px;border-top:1px solid #2a2a4a;padding-top:16px;">
                          Link expires in 1 hour. If you didn't request this, ignore this email.
                          Your password will not be changed.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        await SendAsync(toEmail, displayName, subject, body, ct).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="MimeMessage"/>, connects an SMTP client, and transmits
    /// the message. Exceptions from MailKit are caught, logged, and re-thrown as
    /// <see cref="InvalidOperationException"/> so that SMTP internals are not
    /// surfaced to callers.
    /// </summary>
    private async Task SendAsync(
        string toEmail,
        string toDisplayName,
        string subject,
        string htmlBody,
        CancellationToken ct)
    {
        var message = BuildMessage(toEmail, toDisplayName, subject, htmlBody);

        _logger.LogDebug(
            "Sending email to {ToEmail} via {SmtpHost}:{SmtpPort}",
            toEmail,
            _settings.SmtpHost,
            _settings.SmtpPort);

        try
        {
            using var client = new SmtpClient();

            var socketOptions = _settings.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : _settings.UseStartTls
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions, ct)
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(_settings.SmtpUsername))
            {
                await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, ct)
                    .ConfigureAwait(false);
            }

            await client.SendAsync(message, ct).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Email delivered to {ToEmail} (subject: {Subject})",
                toEmail,
                subject);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "SMTP delivery failed for recipient {ToEmail}", toEmail);
            throw new InvalidOperationException($"Email delivery failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Constructs a <see cref="MimeMessage"/> with the From address populated from
    /// <see cref="EmailSettings"/> and a single HTML body part.
    /// </summary>
    private MimeMessage BuildMessage(
        string toEmail,
        string toDisplayName,
        string subject,
        string htmlBody)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_settings.FromDisplayName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(toDisplayName, toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = builder.ToMessageBody();

        return message;
    }
}
