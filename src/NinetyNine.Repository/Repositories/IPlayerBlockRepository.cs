using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access for <see cref="PlayerBlock"/> documents.
/// Block is bidirectional in queries — checking "is X blocked by Y"
/// also returns true for "X blocked Y".
/// </summary>
public interface IPlayerBlockRepository
{
    /// <summary>
    /// Returns the block between two players (in either direction),
    /// or null if no block exists.
    /// </summary>
    Task<PlayerBlock?> GetBlockAsync(Guid playerA, Guid playerB, CancellationToken ct = default);

    /// <summary>
    /// Returns all player IDs that a given player has blocked or has
    /// been blocked by. Used for filtering search/leaderboard results.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListBlockedIdsAsync(Guid playerId, CancellationToken ct = default);

    Task CreateAsync(PlayerBlock block, CancellationToken ct = default);

    /// <summary>Removes a block. Only the original blocker can unblock.</summary>
    Task DeleteAsync(Guid blockId, CancellationToken ct = default);
}
