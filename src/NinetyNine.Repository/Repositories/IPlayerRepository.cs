using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="Player"/> documents.
/// </summary>
public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(Guid playerId, CancellationToken ct = default);
    Task<Player?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default);
    Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken ct = default);
    Task<IReadOnlyList<Player>> SearchAsync(string query, int limit, CancellationToken ct = default);
    Task CreateAsync(Player player, CancellationToken ct = default);
    Task UpdateAsync(Player player, CancellationToken ct = default);
    Task DeleteAsync(Guid playerId, CancellationToken ct = default);
}
