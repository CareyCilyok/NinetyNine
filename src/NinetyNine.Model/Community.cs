namespace NinetyNine.Model;

/// <summary>
/// A named group of players — the first-class "communities / groups"
/// concept. Can be owned by an individual <see cref="Player"/> (v1.0) or
/// by a <see cref="Venue"/> (v1.1 UI, v1.0 data model).
/// <para>
/// Exactly one of <see cref="OwnerPlayerId"/> / <see cref="OwnerVenueId"/>
/// is non-null, keyed by <see cref="OwnerType"/>. The mutual-exclusion
/// invariant is enforced in the service layer and asserted in tests.
/// </para>
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.2.</para>
/// </summary>
public class Community
{
    public Guid CommunityId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name. Unique case-insensitively across all communities, enforced
    /// by a Mongo collation-strength-2 unique index.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// URL-safe slug derived from <see cref="Name"/> at create time. Unique.
    /// </summary>
    public string Slug { get; set; } = "";

    public string? Description { get; set; }

    /// <summary>
    /// Public or private. A private community is fully hidden from non-members
    /// — the detail page returns 404 rather than revealing existence.
    /// </summary>
    public CommunityVisibility Visibility { get; set; } = CommunityVisibility.Public;

    /// <summary>
    /// Which kind of entity owns this community. Determines which of
    /// <see cref="OwnerPlayerId"/> / <see cref="OwnerVenueId"/> is populated.
    /// </summary>
    public CommunityOwnerType OwnerType { get; set; } = CommunityOwnerType.Player;

    /// <summary>
    /// Owning player, when <see cref="OwnerType"/> is <see cref="CommunityOwnerType.Player"/>.
    /// Must be null otherwise.
    /// </summary>
    public Guid? OwnerPlayerId { get; set; }

    /// <summary>
    /// Owning venue, when <see cref="OwnerType"/> is <see cref="CommunityOwnerType.Venue"/>.
    /// Must be null otherwise. (v1.1 UI; data model exists in v1.0.)
    /// </summary>
    public Guid? OwnerVenueId { get; set; }

    /// <summary>
    /// Player responsible for creating this community, regardless of
    /// <see cref="OwnerType"/>. Always set. For a venue-owned community this
    /// is the venue staff member who initiated the claim — useful for audit.
    /// </summary>
    public Guid CreatedByPlayerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Schema evolution marker. 1 = initial Sprint 0 shape.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Convenience: asserts the owner-type / owner-id mutual-exclusion
    /// invariant. Called by tests and by the service layer on write.
    /// </summary>
    public void AssertOwnerInvariant()
    {
        switch (OwnerType)
        {
            case CommunityOwnerType.Player:
                if (OwnerPlayerId is null)
                    throw new InvalidOperationException(
                        $"Community {CommunityId} has OwnerType=Player but OwnerPlayerId is null.");
                if (OwnerVenueId is not null)
                    throw new InvalidOperationException(
                        $"Community {CommunityId} has OwnerType=Player but OwnerVenueId is set.");
                break;

            case CommunityOwnerType.Venue:
                if (OwnerVenueId is null)
                    throw new InvalidOperationException(
                        $"Community {CommunityId} has OwnerType=Venue but OwnerVenueId is null.");
                if (OwnerPlayerId is not null)
                    throw new InvalidOperationException(
                        $"Community {CommunityId} has OwnerType=Venue but OwnerPlayerId is set.");
                break;

            default:
                throw new InvalidOperationException(
                    $"Community {CommunityId} has unknown OwnerType {OwnerType}.");
        }
    }
}

/// <summary>
/// Visibility of a <see cref="Community"/>. Public communities are listed on
/// the browse page and can be joined by any authenticated user. Private
/// communities are fully hidden from non-members — no name, no member count,
/// no 404 that reveals "this community exists, you just can't see it".
/// </summary>
public enum CommunityVisibility
{
    Public = 0,
    Private = 1,
}

/// <summary>
/// Discriminator for <see cref="Community.OwnerType"/>. Determines which of
/// <see cref="Community.OwnerPlayerId"/> / <see cref="Community.OwnerVenueId"/>
/// is populated.
/// </summary>
public enum CommunityOwnerType
{
    Player = 0,
    Venue = 1,
}
