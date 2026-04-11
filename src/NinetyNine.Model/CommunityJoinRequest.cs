namespace NinetyNine.Model;

/// <summary>
/// An inbound request from a player asking to join a private
/// <see cref="Community"/>. (Public communities allow one-click join — no
/// request needed.) A community Owner or Admin Approves or Denies.
/// <para>
/// Join requests auto-expire 30 days after <see cref="CreatedAt"/> and are
/// swept by the <c>DataSeeder</c> heal pass in Sprint 4.
/// </para>
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.2.</para>
/// </summary>
public class CommunityJoinRequest
{
    public Guid RequestId { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }

    /// <summary>Player asking to join.</summary>
    public Guid PlayerId { get; set; }

    public CommunityJoinRequestStatus Status { get; set; } = CommunityJoinRequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC when the request reached a terminal state.</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>
    /// Community Owner or Admin who approved or denied the request. Null
    /// while <see cref="Status"/> is Pending or the request was self-cancelled.
    /// </summary>
    public Guid? DecidedByPlayerId { get; set; }
}

/// <summary>
/// Lifecycle state of a <see cref="CommunityJoinRequest"/>.
/// </summary>
public enum CommunityJoinRequestStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
    Cancelled = 3,
    Expired = 4,
}
