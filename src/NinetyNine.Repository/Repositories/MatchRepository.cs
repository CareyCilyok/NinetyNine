using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public sealed class MatchRepository(
    INinetyNineDbContext context,
    ILogger<MatchRepository> logger) : IMatchRepository
{
    private readonly IMongoCollection<Match> _collection = context.Matches;

    public async Task CreateAsync(Match match, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        await _collection.InsertOneAsync(match, cancellationToken: ct);
        logger.LogInformation(
            "Created match {MatchId}: {Rotation}/{Format} {Target}, venue {VenueId}, {PlayerCount} players",
            match.MatchId, match.Rotation, match.Format, match.Target,
            match.VenueId, match.PlayerIds.Count);
    }

    public async Task<Match?> GetByIdAsync(Guid matchId, CancellationToken ct = default)
    {
        var filter = Builders<Match>.Filter.Eq(m => m.MatchId, matchId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Match>> ListForPlayerAsync(
        Guid playerId,
        MatchStatus? status = null,
        int skip = 0,
        int limit = 20,
        CancellationToken ct = default)
    {
        var filter = Builders<Match>.Filter.AnyEq(m => m.PlayerIds, playerId);
        if (status is not null)
            filter &= Builders<Match>.Filter.Eq(m => m.Status, status.Value);

        var results = await _collection.Find(filter)
            .SortByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task UpdateAsync(Match match, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        var filter = Builders<Match>.Filter.Eq(m => m.MatchId, match.MatchId);
        await _collection.ReplaceOneAsync(filter, match, cancellationToken: ct);
    }
}
