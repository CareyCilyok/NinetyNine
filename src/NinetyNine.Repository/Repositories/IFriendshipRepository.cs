using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="Friendship"/> documents. A friendship
/// is stored as a single canonically-ordered edge (see <see cref="Friendship.Create"/>)
/// so callers can pass the player ids in any order.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.4.</para>
/// </summary>
public interface IFriendshipRepository
{
    /// <summary>
    /// Returns the friendship between two players, or <c>null</c> if they
    /// are not friends. Argument order does not matter.
    /// </summary>
    Task<Friendship?> GetByPairAsync(Guid playerA, Guid playerB, CancellationToken ct = default);

    /// <summary>
    /// Lists every friendship a player is part of. Results are not ordered
    /// — callers sort by the other party's display name at the service layer.
    /// </summary>
    Task<IReadOnlyList<Friendship>> ListForPlayerAsync(Guid playerId, CancellationToken ct = default);

    /// <summary>
    /// Inserts a friendship. The unique index on
    /// <see cref="Friendship.PlayerIdsKey"/> enforces idempotency — a
    /// duplicate insert throws <see cref="MongoDB.Driver.MongoWriteException"/>.
    /// </summary>
    Task CreateAsync(Friendship friendship, CancellationToken ct = default);

    /// <summary>
    /// Removes the friendship between two players (if it exists). Idempotent:
    /// no exception when the pair is not friends.
    /// </summary>
    Task DeleteAsync(Guid playerA, Guid playerB, CancellationToken ct = default);

    /// <summary>
    /// Returns the number of friendships a player is part of. Used for the
    /// denormalized <c>friendCount</c> maintained by the heal pass.
    /// </summary>
    Task<long> CountForPlayerAsync(Guid playerId, CancellationToken ct = default);
}
