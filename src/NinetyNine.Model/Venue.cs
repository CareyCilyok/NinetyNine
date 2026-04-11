namespace NinetyNine.Model;

/// <summary>
/// A pool hall or other venue where games are played.
/// </summary>
public class Venue
{
    public Guid VenueId { get; set; } = Guid.NewGuid();

    /// <summary>When true, the venue is only visible to its creator.</summary>
    public bool Private { get; set; }

    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    /// <summary>
    /// Optional affiliation with a single <see cref="Community"/>. A venue
    /// belongs to at most one community. Null for unaffiliated venues.
    /// Set via <c>IVenueService.SetCommunityAffiliationAsync</c> (Sprint 3).
    /// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.2.</para>
    /// </summary>
    public Guid? CommunityId { get; set; }

    /// <summary>
    /// Player who originally created this venue. Retroactive field — legacy
    /// seeded venues have this unset (null) and are claimed by the first
    /// editor via <c>IVenueService</c>. Used for authorization on venue
    /// edits and community affiliation.
    /// </summary>
    public Guid? CreatedByPlayerId { get; set; }

    /// <summary>
    /// Schema evolution marker. 1 = pre-Sprint-0 (no CommunityId / CreatedByPlayerId);
    /// 2 = Sprint 0 onwards.
    /// </summary>
    public int SchemaVersion { get; set; } = 2;
}
