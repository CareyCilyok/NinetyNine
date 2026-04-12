using Microsoft.Extensions.Logging;

namespace NinetyNine.Services;

/// <summary>
/// Dev-only stub that logs formatted notification emails to the
/// structured logger rather than sending real email. Mirrors the
/// <c>ConsoleEmailSender</c> pattern from auth.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 5 S5.3.</para>
/// </summary>
public sealed class ConsoleNotificationDeliveryService(
    ILogger<ConsoleNotificationDeliveryService> logger) : INotificationDeliveryService
{
    public Task DeliverAsync(
        string toEmail,
        string displayName,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL STUB] To: {To} ({DisplayName})\n" +
            "  Subject: {Subject}\n" +
            "  Body:\n{Body}",
            toEmail, displayName, subject, body);

        return Task.CompletedTask;
    }
}
