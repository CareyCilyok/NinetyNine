using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public sealed class NotificationRepository(
    INinetyNineDbContext context,
    ILogger<NotificationRepository> logger) : INotificationRepository
{
    private readonly IMongoCollection<Notification> _collection =
        context.Notifications;

    public async Task CreateAsync(Notification notification, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await _collection.InsertOneAsync(notification, cancellationToken: ct);
        logger.LogDebug(
            "Created notification {NotificationId} type={Type} for player {PlayerId}",
            notification.NotificationId, notification.Type, notification.PlayerId);
    }

    public async Task<IReadOnlyList<Notification>> ListForPlayerAsync(
        Guid playerId, int skip = 0, int limit = 50, CancellationToken ct = default)
    {
        var filter = Builders<Notification>.Filter.Eq(n => n.PlayerId, playerId);
        var results = await _collection.Find(filter)
            .SortByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task<long> CountUnreadAsync(Guid playerId, CancellationToken ct = default)
    {
        var filter = Builders<Notification>.Filter.And(
            Builders<Notification>.Filter.Eq(n => n.PlayerId, playerId),
            Builders<Notification>.Filter.Eq(n => n.ReadAt, null));
        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task MarkAllReadAsync(Guid playerId, DateTime readAt, CancellationToken ct = default)
    {
        var filter = Builders<Notification>.Filter.And(
            Builders<Notification>.Filter.Eq(n => n.PlayerId, playerId),
            Builders<Notification>.Filter.Eq(n => n.ReadAt, null));
        var update = Builders<Notification>.Update.Set(n => n.ReadAt, readAt);
        await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}
