using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public interface IVoteRepository
{
    Task CreateAsync(Vote vote, CancellationToken ct = default);
    Task<Vote?> GetByPollAndPlayerAsync(Guid pollId, Guid playerId, CancellationToken ct = default);
    Task<long> CountByPollAsync(Guid pollId, CancellationToken ct = default);
    Task<IReadOnlyList<Vote>> ListByPollAsync(Guid pollId, CancellationToken ct = default);
}
