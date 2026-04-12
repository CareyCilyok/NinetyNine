using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access for <see cref="Notification"/> documents.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 5 S5.2.</para>
/// </summary>
public interface INotificationRepository
{
    Task CreateAsync(Notification notification, CancellationToken ct = default);

    /// <summary>
    /// Lists notifications for a player, newest first. Paged via skip/limit.
    /// </summary>
    Task<IReadOnlyList<Notification>> ListForPlayerAsync(
        Guid playerId, int skip = 0, int limit = 50, CancellationToken ct = default);

    /// <summary>Counts unread notifications for a player.</summary>
    Task<long> CountUnreadAsync(Guid playerId, CancellationToken ct = default);

    /// <summary>
    /// Marks all unread notifications for a player as read at the given timestamp.
    /// </summary>
    Task MarkAllReadAsync(Guid playerId, DateTime readAt, CancellationToken ct = default);
}
