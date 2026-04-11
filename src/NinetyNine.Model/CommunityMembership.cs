namespace NinetyNine.Model;

/// <summary>
/// A player's membership in a <see cref="Community"/>. Stored in a dedicated
/// join collection (not embedded on either side) so that both directions are
/// cheap to query:
/// <list type="bullet">
/// <item>Members of a community, ordered by <see cref="JoinedAt"/>.</item>
/// <item>Communities a given player belongs to.</item>
/// </list>
/// The pair <c>{CommunityId, PlayerId}</c> is unique, enforced by a Mongo index.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.2.</para>
/// </summary>
public class CommunityMembership
{
    public Guid MembershipId { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }

    public Guid PlayerId { get; set; }

    /// <summary>
    /// Role within the community. Owner is singular; Admin is v1.1; Member
    /// is the default.
    /// </summary>
    public CommunityRole Role { get; set; } = CommunityRole.Member;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Player who invited the new member, if any. Null for self-joins
    /// (public community "Join" button) and owner membership created at
    /// community-creation time.
    /// </summary>
    public Guid? InvitedByPlayerId { get; set; }
}

/// <summary>
/// Role within a <see cref="Community"/>. Permissions are enforced in the
/// service layer per the security auth matrix in the plan.
/// </summary>
public enum CommunityRole
{
    Member = 0,
    Admin = 1,
    Owner = 2,
}
