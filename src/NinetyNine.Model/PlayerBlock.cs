namespace NinetyNine.Model;

/// <summary>
/// A bidirectional block between two players. When a block exists,
/// neither party sees the other in search results, leaderboards,
/// or community member lists.
/// <para>
/// Blocking auto-unfriends and auto-removes the blocked player from
/// any shared private community where the blocker has removal rights.
/// Unblocking reverses the list filtering but does NOT restore
/// friendships or memberships.
/// </para>
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 5 S5.4.</para>
/// </summary>
public class PlayerBlock
{
    public Guid BlockId { get; set; } = Guid.NewGuid();

    /// <summary>The player who initiated the block.</summary>
    public Guid BlockerPlayerId { get; set; }

    /// <summary>The player who is blocked.</summary>
    public Guid BlockedPlayerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
