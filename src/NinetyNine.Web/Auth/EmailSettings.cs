namespace NinetyNine.Web.Auth;

/// <summary>
/// Strongly-typed configuration model for the email delivery subsystem.
/// Bind this from the <c>Email</c> configuration section (e.g. appsettings.json
/// or environment variables). WP-05 wires the binding in DI.
/// </summary>
public sealed class EmailSettings
{
    /// <summary>
    /// Selects the active <see cref="IEmailSender"/> implementation.
    /// Accepted values: <c>"MailKit"</c>, <c>"Console"</c>, or <c>"Mock"</c>.
    /// Defaults to <c>"Console"</c> so development boxes never need SMTP config.
    /// </summary>
    public string Provider { get; set; } = "Console";

    /// <summary>Hostname or IP address of the outbound SMTP relay.</summary>
    public string SmtpHost { get; set; } = "";

    /// <summary>
    /// TCP port used to connect to the SMTP relay.
    /// Typical values: 587 (StartTLS, default), 465 (implicit TLS), 25 (plain/legacy).
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>SMTP authentication username. Leave empty to skip AUTH.</summary>
    public string SmtpUsername { get; set; } = "";

    /// <summary>SMTP authentication password. Store in secrets — never commit plain text.</summary>
    public string SmtpPassword { get; set; } = "";

    /// <summary>RFC 5321 envelope / header From address (e.g. <c>no-reply@example.com</c>).</summary>
    public string FromAddress { get; set; } = "";

    /// <summary>Human-readable sender name shown in email clients.</summary>
    public string FromDisplayName { get; set; } = "NinetyNine";

    /// <summary>
    /// When <see langword="true"/>, upgrades the plain connection with STARTTLS (port 587 convention).
    /// Set to <see langword="false"/> to use implicit TLS on port 465, or no TLS on port 25.
    /// Ignored when <see cref="SmtpPort"/> is 465 (always uses implicit TLS).
    /// </summary>
    public bool UseStartTls { get; set; } = true;
}
