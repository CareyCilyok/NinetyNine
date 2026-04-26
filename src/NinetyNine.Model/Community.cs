namespace NinetyNine.Model;

/// <summary>
/// A named group of pool players — the first-class "communities / groups"
/// concept. Always owned by a single pool player (see
/// <see cref="OwnerPlayerId"/>).
/// <para>
/// <b>Pool players only principle:</b> Venues can be <i>affiliated</i> with
/// a community via <see cref="Venue.CommunityId"/> but cannot own, admin,
/// or govern one. Future group decisions go through a polling/voting
/// feature restricted to pool players. See the plan's 2026-04-11
/// changelog entry and
/// <c>.claude/.../memory/project-pool-players-only.md</c>.
/// </para>
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.2
/// (and the 2026-04-11 fork-B reversal).</para>
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
    /// The pool player who owns this community. Always set; a community
    /// cannot exist without a human owner.
    /// </summary>
    public Guid OwnerPlayerId { get; set; }

    /// <summary>
    /// Pool player who originally created this community. Always set, and
    /// for player-owned communities it equals <see cref="OwnerPlayerId"/>
    /// at create time — diverges only after ownership transfer.
    /// </summary>
    public Guid CreatedByPlayerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional parent community. <c>null</c> means this community is a
    /// root in the hierarchy. The seeded "Global" community is the
    /// canonical root; every other community in the seed dataset (and
    /// every community surfaced from <c>/communities/new</c> by default)
    /// is a child of Global.
    /// <para>
    /// Cycles are forbidden — <c>ICommunityService.SetParentAsync</c>
    /// validates that setting a new parent does not place the community
    /// in its own ancestor chain. The repository never enforces this on
    /// writes (callers must go through the service).
    /// </para>
    /// <para>
    /// Pre-v3 documents have no <c>parentCommunityId</c> field at all;
    /// they deserialize as null (= root) which is the correct
    /// pre-hierarchy reading. The v0.8.0 dev seeder reconciles legacy
    /// seeded communities (Pocket Sports + the 5 themed mock
    /// communities) under Global on first startup after the upgrade.
    /// </para>
    /// </summary>
    public Guid? ParentCommunityId { get; set; }

    /// <summary>
    /// Schema evolution marker. 1 = initial Sprint 0 shape (with the
    /// `OwnerType` discriminator and `OwnerVenueId`); 2 = Sprint 3
    /// principle update that removed both; 3 = v0.8.0 hierarchy
    /// (added <see cref="ParentCommunityId"/>). The class map ignores
    /// extra elements, so legacy docs still deserialize cleanly.
    /// </summary>
    public int SchemaVersion { get; set; } = 3;
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
