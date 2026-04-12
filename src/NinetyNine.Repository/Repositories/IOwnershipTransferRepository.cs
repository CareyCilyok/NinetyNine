using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access for <see cref="OwnershipTransfer"/> documents.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 4 S4.3.</para>
/// </summary>
public interface IOwnershipTransferRepository
{
    Task<OwnershipTransfer?> GetByIdAsync(Guid transferId, CancellationToken ct = default);

    /// <summary>
    /// Returns the active Pending transfer for a community, or null.
    /// At most one can be Pending per community.
    /// </summary>
    Task<OwnershipTransfer?> GetPendingByCommunityAsync(Guid communityId, CancellationToken ct = default);

    /// <summary>
    /// Returns any Pending transfer where the given player is the target.
    /// Used for rendering the accept/decline UI.
    /// </summary>
    Task<IReadOnlyList<OwnershipTransfer>> ListPendingForTargetAsync(Guid toPlayerId, CancellationToken ct = default);

    Task CreateAsync(OwnershipTransfer transfer, CancellationToken ct = default);
    Task UpdateAsync(OwnershipTransfer transfer, CancellationToken ct = default);
}
