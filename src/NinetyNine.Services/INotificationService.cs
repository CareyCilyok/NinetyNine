using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Creates and reads in-app notifications. Other services call
/// <see cref="NotifyAsync"/> when high-signal events occur; the
/// <c>/notifications</c> page calls the read methods.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 5 S5.2.</para>
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates a notification for a player. Typically called by
    /// FriendService, CommunityService, etc. when events occur.
    /// </summary>
    Task NotifyAsync(
        Guid playerId,
        string type,
        string message,
        string? linkUrl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists notifications for a player, newest first.
    /// </summary>
    Task<IReadOnlyList<Notification>> ListAsync(
        Guid playerId, int skip = 0, int limit = 50, CancellationToken ct = default);

    /// <summary>Counts unread notifications for a player.</summary>
    Task<long> CountUnreadAsync(Guid playerId, CancellationToken ct = default);

    /// <summary>Marks all unread notifications as read.</summary>
    Task MarkAllReadAsync(Guid playerId, CancellationToken ct = default);
}
