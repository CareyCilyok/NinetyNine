using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public sealed class VoteRepository(
    INinetyNineDbContext context,
    ILogger<VoteRepository> logger) : IVoteRepository
{
    private readonly IMongoCollection<Vote> _collection = context.Votes;

    public async Task CreateAsync(Vote vote, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vote);
        await _collection.InsertOneAsync(vote, cancellationToken: ct);
        logger.LogDebug("Vote {VoteId} cast on poll {PollId}", vote.VoteId, vote.PollId);
    }

    public async Task<Vote?> GetByPollAndPlayerAsync(
        Guid pollId, Guid playerId, CancellationToken ct = default)
    {
        var filter = Builders<Vote>.Filter.And(
            Builders<Vote>.Filter.Eq(v => v.PollId, pollId),
            Builders<Vote>.Filter.Eq(v => v.PlayerId, playerId));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<long> CountByPollAsync(Guid pollId, CancellationToken ct = default)
    {
        var filter = Builders<Vote>.Filter.Eq(v => v.PollId, pollId);
        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Vote>> ListByPollAsync(
        Guid pollId, CancellationToken ct = default)
    {
        var filter = Builders<Vote>.Filter.Eq(v => v.PollId, pollId);
        var results = await _collection.Find(filter).ToListAsync(ct);
        return results.AsReadOnly();
    }
}
