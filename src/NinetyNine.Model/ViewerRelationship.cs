namespace NinetyNine.Model;

/// <summary>
/// The viewer's relationship to a target player when rendering that
/// player's profile. Ordered so that each viewer's numeric value is the
/// <em>floor audience tier</em> they qualify for — the tightest audience
/// that still reveals the field to them.
/// <para>
/// A field is visible to the viewer when
/// <c>(int)viewerRelationship &lt;= (int)fieldAudience</c>, i.e., "the
/// viewer's floor is at or below the field's audience tier". Because
/// <see cref="Audience"/> is ordered most-private-first
/// (<c>Private = 0, Friends = 1, Communities = 2, Public = 3</c>),
/// the relationship enum uses the same numeric space so a single
/// integer comparison resolves every case.
/// </para>
/// <para>
/// Resolution order when computing relationship (first match wins):
/// <list type="number">
///   <item>Self — <c>viewerId == targetId</c>.</item>
///   <item>Friend — a row exists in <c>friendships</c> for the pair.</item>
///   <item>CommunityMember — the viewer and target share at least one
///     community as approved members.</item>
///   <item>Public — any other authenticated NinetyNine user.</item>
///   <item>Anonymous — viewer is unauthenticated.</item>
/// </list>
/// </para>
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 3 S3.5.</para>
/// </summary>
public enum ViewerRelationship
{
    /// <summary>
    /// Unauthenticated viewer. Does not participate in the integer
    /// comparison — callers treat <see cref="Anonymous"/> specially and
    /// reveal only the display name plus the avatar when
    /// <see cref="Audience.Public"/>.
    /// </summary>
    Anonymous = -1,

    /// <summary>
    /// Viewer is the target. Floor = <see cref="Audience.Private"/> (0),
    /// so every field tier is visible (<c>0 &lt;= anyAudience</c>).
    /// </summary>
    Self = 0,

    /// <summary>
    /// Viewer and target have an accepted, mutual friendship. Floor =
    /// <see cref="Audience.Friends"/> (1) — reveals fields whose audience
    /// is Friends, Communities, or Public, but not Private.
    /// </summary>
    Friend = 1,

    /// <summary>
    /// Viewer and target share at least one community where both are
    /// approved members. Floor = <see cref="Audience.Communities"/> (2) —
    /// reveals Communities- and Public-tier fields only.
    /// </summary>
    CommunityMember = 2,

    /// <summary>
    /// Viewer is any other authenticated NinetyNine user. Floor =
    /// <see cref="Audience.Public"/> (3) — reveals only Public-tier fields.
    /// </summary>
    Public = 3,
}
