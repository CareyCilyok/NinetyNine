using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for the <c>community_members</c> join collection.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.4.</para>
/// </summary>
public interface ICommunityMemberRepository
{
    Task<CommunityMembership?> GetMembershipAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists the members of a community ordered by join time ascending.
    /// Caller is responsible for pagination (skip/limit).
    /// </summary>
    Task<IReadOnlyList<CommunityMembership>> ListMembersAsync(
        Guid communityId,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Returns every community a player currently belongs to. Order is not
    /// guaranteed; service layer sorts.
    /// </summary>
    Task<IReadOnlyList<CommunityMembership>> ListCommunitiesForPlayerAsync(
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>
    /// Inserts a new membership. The unique index on
    /// <c>{PlayerId, CommunityId}</c> enforces idempotency.
    /// </summary>
    Task AddAsync(CommunityMembership membership, CancellationToken ct = default);

    /// <summary>Removes a membership. Idempotent.</summary>
    Task RemoveAsync(Guid communityId, Guid playerId, CancellationToken ct = default);

    /// <summary>Number of members currently in a community.</summary>
    Task<long> CountMembersAsync(Guid communityId, CancellationToken ct = default);
}
