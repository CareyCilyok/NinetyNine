namespace NinetyNine.Services.Tests;

using NinetyNine.Services.Auth;

/// <summary>
/// In-memory test double for <see cref="IEmailSender"/> that records every call so
/// tests can assert on email delivery without sending real messages.
/// Thread-safe for parallel test execution.
/// </summary>
public sealed class TestEmailSender : IEmailSender
{
    public sealed record SentEmail(string Kind, string To, string DisplayName, string Url);

    private readonly List<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> SentEmails
    {
        get { lock (_sent) return _sent.ToList(); }
    }

    public Task SendVerificationAsync(
        string toEmail,
        string displayName,
        string verifyUrl,
        CancellationToken ct = default)
    {
        lock (_sent) _sent.Add(new("Verification", toEmail, displayName, verifyUrl));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string displayName,
        string resetUrl,
        CancellationToken ct = default)
    {
        lock (_sent) _sent.Add(new("PasswordReset", toEmail, displayName, resetUrl));
        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_sent) _sent.Clear();
    }
}
