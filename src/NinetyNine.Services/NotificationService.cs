using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

public sealed class NotificationService(
    INotificationRepository notifications,
    ILogger<NotificationService> logger) : INotificationService
{
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
