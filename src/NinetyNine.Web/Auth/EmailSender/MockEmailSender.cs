namespace NinetyNine.Web.Auth.EmailSender;

/// <summary>
/// Test-mode <see cref="IEmailSender"/> that accumulates sent emails in an
/// in-memory list rather than performing any I/O.
/// </summary>
/// <remarks>
/// Designed for use in integration tests (WP-10). After arranging the scenario,
/// assert against <see cref="SentEmails"/> to verify that the expected emails
/// were dispatched with the correct parameters.
/// Thread-safe: all mutations are guarded by a lock on <see cref="SentEmails"/>.
/// </remarks>
public sealed class MockEmailSender : IEmailSender
{
    /// <summary>
    /// Represents a single email captured by <see cref="MockEmailSender"/>.
    /// </summary>
    /// <param name="Kind">
    /// The type of email: <c>"Verification"</c> or <c>"PasswordReset"</c>.
    /// </param>
    /// <param name="ToEmail">The recipient address passed to the sending method.</param>
    /// <param name="DisplayName">The display name passed to the sending method.</param>
    /// <param name="Url">The verification or reset URL passed to the sending method.</param>
    /// <param name="SentAt">UTC timestamp recorded at the moment <see cref="SentEmails"/> was appended.</param>
    public sealed record SentEmail(
        string Kind,
        string ToEmail,
        string DisplayName,
        string Url,
        DateTime SentAt);

    /// <summary>
    /// All emails captured since construction or the last <see cref="Clear"/> call.
    /// Do not add to this list directly from tests; use it only for assertions.
    /// </summary>
    public List<SentEmail> SentEmails { get; } = new();

    /// <summary>
    /// Removes all captured emails. Call this in test setup (<c>BeforeEach</c> / constructor)
    /// to ensure a clean slate between test cases.
    /// </summary>
    public void Clear()
    {
        lock (SentEmails)
        {
            SentEmails.Clear();
        }
    }

    /// <inheritdoc />
    /// <remarks>Appends a <see cref="SentEmail"/> with <c>Kind = "Verification"</c>. No I/O is performed.</remarks>
    public Task SendVerificationAsync(
        string toEmail,
        string displayName,
        string verifyUrl,
        CancellationToken ct)
    {
        lock (SentEmails)
        {
            SentEmails.Add(new SentEmail(
                Kind: "Verification",
                ToEmail: toEmail,
                DisplayName: displayName,
                Url: verifyUrl,
                SentAt: DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>Appends a <see cref="SentEmail"/> with <c>Kind = "PasswordReset"</c>. No I/O is performed.</remarks>
    public Task SendPasswordResetAsync(
        string toEmail,
        string displayName,
        string resetUrl,
        CancellationToken ct)
    {
        lock (SentEmails)
        {
            SentEmails.Add(new SentEmail(
                Kind: "PasswordReset",
                ToEmail: toEmail,
                DisplayName: displayName,
                Url: resetUrl,
                SentAt: DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }
}
