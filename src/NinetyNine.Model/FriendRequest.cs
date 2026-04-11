namespace NinetyNine.Model;

/// <summary>
/// A directed friend request from one player to another. Lifecycle:
/// <list type="bullet">
/// <item><see cref="FriendRequestStatus.Pending"/> — initial state, awaiting response.</item>
/// <item><see cref="FriendRequestStatus.Accepted"/> — terminal; a <see cref="Friendship"/> exists.</item>
/// <item><see cref="FriendRequestStatus.Declined"/> — terminal; 90-day re-request cooldown applies.</item>
/// <item><see cref="FriendRequestStatus.Cancelled"/> — terminal; cancelled by the sender.</item>
/// <item><see cref="FriendRequestStatus.Expired"/> — terminal; swept by heal pass after 30 days pending.</item>
/// </list>
/// A unique partial index (on <c>{FromPlayerId, ToPlayerId}</c> where
/// <c>Status = Pending</c>) prevents duplicate pending requests.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.2.</para>
/// </summary>
public class FriendRequest
{
    public Guid RequestId { get; set; } = Guid.NewGuid();

    /// <summary>Player who sent the request.</summary>
    public Guid FromPlayerId { get; set; }

    /// <summary>Player who received the request.</summary>
    public Guid ToPlayerId { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;

    /// <summary>Optional short note from the sender. Max 280 chars.</summary>
    public string? Message { get; set; }

    /// <summary>UTC timestamp when the request was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the request reached a terminal state. Null while
    /// <see cref="Status"/> is <see cref="FriendRequestStatus.Pending"/>.
    /// </summary>
    public DateTime? RespondedAt { get; set; }
}

/// <summary>
/// Lifecycle state of a <see cref="FriendRequest"/>. All non-Pending values
/// are terminal and immutable; a new request must be created to re-try.
/// </summary>
public enum FriendRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Cancelled = 3,
    Expired = 4,
}
