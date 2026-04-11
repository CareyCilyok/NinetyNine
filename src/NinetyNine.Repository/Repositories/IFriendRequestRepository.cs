using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="FriendRequest"/> documents. The
/// partial unique index on <c>{FromPlayerId, ToPlayerId}</c> where
/// <c>Status = Pending</c> prevents duplicate pending requests; terminal
/// states (Accepted/Declined/Cancelled/Expired) can coexist.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.4.</para>
/// </summary>
public interface IFriendRequestRepository
{
    /// <summary>Returns a request by id, or null if missing.</summary>
    Task<FriendRequest?> GetByIdAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Returns the pending request in the given direction (from → to), if any.
    /// Does not look at the reverse direction.
    /// </summary>
    Task<FriendRequest?> GetPendingAsync(Guid fromPlayerId, Guid toPlayerId, CancellationToken ct = default);

    /// <summary>
    /// Lists incoming requests for a player, optionally filtered by status.
    /// Sorted by created-at descending.
    /// </summary>
    Task<IReadOnlyList<FriendRequest>> ListIncomingAsync(
        Guid playerId,
        FriendRequestStatus? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists outgoing requests from a player, optionally filtered by status.
    /// Sorted by created-at descending.
    /// </summary>
    Task<IReadOnlyList<FriendRequest>> ListOutgoingAsync(
        Guid playerId,
        FriendRequestStatus? status = null,
        CancellationToken ct = default);

    /// <summary>Inserts a new request in Pending state.</summary>
    Task CreateAsync(FriendRequest request, CancellationToken ct = default);

    /// <summary>
    /// Transitions a request to a terminal state, stamping RespondedAt.
    /// No-op if the request is already in the target state.
    /// </summary>
    Task UpdateStatusAsync(
        Guid requestId,
        FriendRequestStatus status,
        DateTime respondedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Expires all pending requests created before the given cutoff. Used by
    /// the <c>DataSeeder</c> heal pass. Returns the number updated.
    /// </summary>
    Task<long> SweepExpiredAsync(DateTime olderThan, CancellationToken ct = default);
}
