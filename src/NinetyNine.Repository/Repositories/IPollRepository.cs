using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public interface IPollRepository
{
    Task CreateAsync(Poll poll, CancellationToken ct = default);
    Task<Poll?> GetByIdAsync(Guid pollId, CancellationToken ct = default);
    Task<IReadOnlyList<Poll>> ListByCommunityAsync(Guid communityId, PollStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyList<Poll>> ListSiteWideAsync(PollStatus? status = null, CancellationToken ct = default);
    Task UpdateAsync(Poll poll, CancellationToken ct = default);
}
