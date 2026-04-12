using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public interface IMatchRepository
{
    Task CreateAsync(Match match, CancellationToken ct = default);
    Task<Match?> GetByIdAsync(Guid matchId, CancellationToken ct = default);
    Task<IReadOnlyList<Match>> ListForPlayerAsync(
        Guid playerId,
        MatchStatus? status = null,
        int skip = 0,
        int limit = 20,
        CancellationToken ct = default);
    Task UpdateAsync(Match match, CancellationToken ct = default);
}
