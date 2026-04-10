using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="Game"/> documents.
/// </summary>
public interface IGameRepository
{
    Task<Game?> GetByIdAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetByPlayerAsync(Guid playerId, int skip, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetRecentAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetCompletedByPlayerAsync(Guid playerId, CancellationToken ct = default);
    Task<Game?> GetActiveForPlayerAsync(Guid playerId, CancellationToken ct = default);
    Task CreateAsync(Game game, CancellationToken ct = default);
    Task UpdateAsync(Game game, CancellationToken ct = default);
    Task DeleteAsync(Guid gameId, CancellationToken ct = default);
}
