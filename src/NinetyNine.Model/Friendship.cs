namespace NinetyNine.Model;

/// <summary>
/// A mutual, accepted friendship between two players. Stored as a single
/// canonically-ordered edge document (not two directed rows) so that the
/// <c>{PlayerAId, PlayerBId}</c> pair is the natural unique key.
/// <para>
/// <see cref="PlayerAId"/> is always the lexicographically-smaller Guid
/// and <see cref="PlayerBId"/> is always the larger. <see cref="PlayerIdsKey"/>
/// is a derived <c>"{a}:{b}"</c> string used as a unique index.
/// </para>
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.2.</para>
/// </summary>
public class Friendship
{
    public Guid FriendshipId { get; set; } = Guid.NewGuid();

    /// <summary>The lexicographically-smaller player Guid.</summary>
    public Guid PlayerAId { get; set; }

    /// <summary>The lexicographically-larger player Guid.</summary>
    public Guid PlayerBId { get; set; }

    /// <summary>
    /// Derived unique key, <c>"{PlayerAId}:{PlayerBId}"</c>, enforced by a
    /// unique Mongo index.
    /// </summary>
    public string PlayerIdsKey { get; set; } = "";

    /// <summary>UTC timestamp when the friendship was accepted.</summary>
    public DateTime Since { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional provenance hint: <c>"request"</c> when created via an accepted
    /// <see cref="FriendRequest"/>, <c>"seeder"</c> when created by
    /// <c>DataSeeder</c>. Purely informational.
    /// </summary>
    public string? CreatedVia { get; set; }

    /// <summary>
    /// Player who initiated the relationship (sent the request that led here).
    /// Null for seeded friendships where there was no request.
    /// </summary>
    public Guid? InitiatedByPlayerId { get; set; }

    /// <summary>
    /// Canonically builds a <see cref="Friendship"/> from two player Guids,
    /// swapping them if needed so <see cref="PlayerAId"/> is the smaller one.
    /// </summary>
    public static Friendship Create(Guid x, Guid y, Guid? initiatedBy = null, string? via = null)
    {
        if (x == y)
            throw new ArgumentException("A player cannot be friends with themselves.", nameof(y));

        var (a, b) = x.CompareTo(y) < 0 ? (x, y) : (y, x);
        return new Friendship
        {
            PlayerAId = a,
            PlayerBId = b,
            PlayerIdsKey = $"{a}:{b}",
            InitiatedByPlayerId = initiatedBy,
            CreatedVia = via,
        };
    }

    /// <summary>
    /// Returns the "other" player in this friendship, given one of the two.
    /// Throws if the supplied id is not a participant.
    /// </summary>
    public Guid OtherParty(Guid playerId)
    {
        if (playerId == PlayerAId) return PlayerBId;
        if (playerId == PlayerBId) return PlayerAId;
        throw new ArgumentException(
            $"Player {playerId} is not a participant in friendship {FriendshipId}.",
            nameof(playerId));
    }
}
