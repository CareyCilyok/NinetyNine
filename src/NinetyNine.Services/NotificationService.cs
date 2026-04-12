using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

public sealed class NotificationService(
    INotificationRepository notifications,
    IPlayerRepository players,
    INotificationDeliveryService delivery,
    ILogger<NotificationService> logger) : INotificationService
{
    /// <summary>
    /// High-signal event types that also trigger an email delivery stub.
    /// </summary>
    private static readonly HashSet<string> EmailableTypes = new(StringComparer.Ordinal)
    {
        "FriendRequestReceived",
        "CommunityInvitationReceived",
        "OwnershipTransferPending",
    };

    public async Task NotifyAsync(
        Guid playerId, string type, string message,
        string? linkUrl = null, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            PlayerId = playerId,
            Type = type,
            Message = message,
            LinkUrl = linkUrl,
        };
        await notifications.CreateAsync(notification, ct);

        // High-signal events also get an email delivery (stub in dev).
        if (EmailableTypes.Contains(type))
        {
            var player = await players.GetByIdAsync(playerId, ct);
            if (player is not null && !string.IsNullOrWhiteSpace(player.EmailAddress))
            {
                await delivery.DeliverAsync(
                    player.EmailAddress,
                    player.DisplayName,
                    $"NinetyNine: {type}",
                    message,
                    ct);
            }
        }
    }

    public Task<IReadOnlyList<Notification>> ListAsync(
        Guid playerId, int skip = 0, int limit = 50, CancellationToken ct = default)
        => notifications.ListForPlayerAsync(playerId, skip, limit, ct);

    public Task<long> CountUnreadAsync(Guid playerId, CancellationToken ct = default)
        => notifications.CountUnreadAsync(playerId, ct);

    public async Task MarkAllReadAsync(Guid playerId, CancellationToken ct = default)
    {
        await notifications.MarkAllReadAsync(playerId, DateTime.UtcNow, ct);
        logger.LogDebug("Marked all notifications read for player {PlayerId}", playerId);
    }
}
