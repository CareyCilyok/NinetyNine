namespace NinetyNine.Model;

/// <summary>
/// An in-app notification for a player. Written by the service layer
/// when high-signal events occur (friend request, community invitation,
/// ownership transfer). Read via <c>/notifications</c> and marked read
/// on view.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 5 S5.2.</para>
/// </summary>
public class Notification
{
    public Guid NotificationId { get; set; } = Guid.NewGuid();

    /// <summary>The player who receives this notification.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Discriminator for the notification template. Examples:
    /// <c>"FriendRequestReceived"</c>, <c>"CommunityInvitationReceived"</c>,
    /// <c>"OwnershipTransferPending"</c>.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Human-readable summary. Rendered as the notification body.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Optional link the notification navigates to on click.
    /// Example: <c>/communities/{id}</c>.
    /// </summary>
    public string? LinkUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Null until the player views the notification. Set to the
    /// timestamp of first view.
    /// </summary>
    public DateTime? ReadAt { get; set; }
}
