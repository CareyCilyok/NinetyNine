using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="CommunityJoinRequest"/> documents.
/// Join requests are inbound (player → private community) and require
/// approval by the community owner (or admin in v1.1+).
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 2 S2.1.</para>
/// </summary>
public interface ICommunityJoinRequestRepository
{
    Task<CommunityJoinRequest?> GetByIdAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Returns the single Pending join request a player has outstanding
    /// to a community, if any. Used to prevent duplicates.
    /// </summary>
    Task<CommunityJoinRequest?> GetPendingAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists Pending join requests awaiting approval at a specific
    /// community. Sorted oldest-first so the owner can act FIFO.
    /// </summary>
    Task<IReadOnlyList<CommunityJoinRequest>> ListPendingByCommunityAsync(
        Guid communityId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists Pending outgoing join requests submitted by a player
    /// (their own side of the inbox).
    /// </summary>
    Task<IReadOnlyList<CommunityJoinRequest>> ListPendingByPlayerAsync(
        Guid playerId,
        CancellationToken ct = default);

    Task CreateAsync(CommunityJoinRequest request, CancellationToken ct = default);

    /// <summary>
    /// Transitions a request to a terminal state, stamping
    /// <c>DecidedAt</c> and <c>DecidedByPlayerId</c>.
    /// </summary>
    Task UpdateStatusAsync(
        Guid requestId,
        CommunityJoinRequestStatus status,
        DateTime decidedAt,
        Guid? decidedByPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Expires Pending requests older than the cutoff. Returns how many
    /// were updated.
    /// </summary>
    Task<long> SweepExpiredAsync(DateTime olderThan, CancellationToken ct = default);
}
