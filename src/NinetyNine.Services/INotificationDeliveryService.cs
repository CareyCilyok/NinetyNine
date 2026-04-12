namespace NinetyNine.Services;

/// <summary>
/// Delivers notifications through external channels (email, push, etc.).
/// The default implementation logs formatted emails to the console via
/// the structured logger — same pattern as
/// <see cref="Auth.IEmailSender"/> / <c>ConsoleEmailSender</c>.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 5 S5.3.</para>
/// </summary>
public interface INotificationDeliveryService
{
    Task DeliverAsync(
        string toEmail,
        string displayName,
        string subject,
        string body,
        CancellationToken ct = default);
}
