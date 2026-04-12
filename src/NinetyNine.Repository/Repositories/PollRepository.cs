using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public sealed class PollRepository(
    INinetyNineDbContext context,
    ILogger<PollRepository> logger) : IPollRepository
{
    private readonly IMongoCollection<Poll> _collection = context.Polls;

    public async Task CreateAsync(Poll poll, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(poll);
        await _collection.InsertOneAsync(poll, cancellationToken: ct);
        logger.LogInformation("Created poll {PollId} '{Title}'", poll.PollId, poll.Title);
    }

    public async Task<Poll?> GetByIdAsync(Guid pollId, CancellationToken ct = default)
    {
        var filter = Builders<Poll>.Filter.Eq(p => p.PollId, pollId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Poll>> ListByCommunityAsync(
        Guid communityId, PollStatus? status = null, CancellationToken ct = default)
    {
        var filter = Builders<Poll>.Filter.Eq(p => p.CommunityId, (Guid?)communityId);
        if (status is not null)
            filter &= Builders<Poll>.Filter.Eq(p => p.Status, status.Value);

        var results = await _collection.Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<Poll>> ListSiteWideAsync(
        PollStatus? status = null, CancellationToken ct = default)
    {
        var filter = Builders<Poll>.Filter.Eq(p => p.CommunityId, (Guid?)null);
        if (status is not null)
            filter &= Builders<Poll>.Filter.Eq(p => p.Status, status.Value);

        var results = await _collection.Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task UpdateAsync(Poll poll, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(poll);
        var filter = Builders<Poll>.Filter.Eq(p => p.PollId, poll.PollId);
        await _collection.ReplaceOneAsync(filter, poll, cancellationToken: ct);
    }
}
