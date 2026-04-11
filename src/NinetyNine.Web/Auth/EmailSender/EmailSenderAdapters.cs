using ServicesIEmailSender = NinetyNine.Services.Auth.IEmailSender;
using WebIEmailSender = NinetyNine.Web.Auth.EmailSender.IEmailSender;

namespace NinetyNine.Web.Auth.EmailSender;

/// <summary>
/// Adapts the Web-layer <see cref="WebIEmailSender"/> implementations to the
/// <see cref="ServicesIEmailSender"/> interface that <c>NinetyNine.Services.Auth.AuthService</c>
/// depends on. Required because the Services project cannot reference the Web project
/// (that would create a circular dependency).
/// </summary>
internal sealed class MailKitEmailSenderAdapter : ServicesIEmailSender
{
    private readonly MailKitEmailSender _inner;

    internal MailKitEmailSenderAdapter(MailKitEmailSender inner) => _inner = inner;

    public Task SendVerificationAsync(string toEmail, string displayName, string verifyUrl, CancellationToken ct)
        => _inner.SendVerificationAsync(toEmail, displayName, verifyUrl, ct);

    public Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, CancellationToken ct)
        => _inner.SendPasswordResetAsync(toEmail, displayName, resetUrl, ct);
}

internal sealed class ConsoleEmailSenderAdapter : ServicesIEmailSender
{
    private readonly ConsoleEmailSender _inner;

    internal ConsoleEmailSenderAdapter(ConsoleEmailSender inner) => _inner = inner;

    public Task SendVerificationAsync(string toEmail, string displayName, string verifyUrl, CancellationToken ct)
        => _inner.SendVerificationAsync(toEmail, displayName, verifyUrl, ct);

    public Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, CancellationToken ct)
        => _inner.SendPasswordResetAsync(toEmail, displayName, resetUrl, ct);
}

internal sealed class MockEmailSenderAdapter : ServicesIEmailSender
{
    private readonly MockEmailSender _inner;

    internal MockEmailSenderAdapter(MockEmailSender inner) => _inner = inner;

    /// <summary>Exposes the underlying <see cref="MockEmailSender"/> for test assertions.</summary>
    internal MockEmailSender Inner => _inner;

    public Task SendVerificationAsync(string toEmail, string displayName, string verifyUrl, CancellationToken ct)
        => _inner.SendVerificationAsync(toEmail, displayName, verifyUrl, ct);

    public Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, CancellationToken ct)
        => _inner.SendPasswordResetAsync(toEmail, displayName, resetUrl, ct);
}
