using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="IFriendshipRepository"/>.
/// </summary>
public sealed class FriendshipRepository(
    INinetyNineDbContext context,
    ILogger<FriendshipRepository> logger) : IFriendshipRepository
{
    private readonly IMongoCollection<Friendship> _collection = context.Friendships;

    public async Task<Friendship?> GetByPairAsync(Guid playerA, Guid playerB, CancellationToken ct = default)
    {
        var key = CanonicalKey(playerA, playerB);
        var filter = Builders<Friendship>.Filter.Eq(f => f.PlayerIdsKey, key);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Friendship>> ListForPlayerAsync(Guid playerId, CancellationToken ct = default)
    {
        var filter = Builders<Friendship>.Filter.Or(
            Builders<Friendship>.Filter.Eq(f => f.PlayerAId, playerId),
            Builders<Friendship>.Filter.Eq(f => f.PlayerBId, playerId));

        var results = await _collection.Find(filter).ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task CreateAsync(Friendship friendship, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(friendship);

        if (string.IsNullOrEmpty(friendship.PlayerIdsKey))
            friendship.PlayerIdsKey = $"{friendship.PlayerAId}:{friendship.PlayerBId}";

        logger.LogInformation(
            "Creating friendship {FriendshipId} between {A} and {B}",
            friendship.FriendshipId, friendship.PlayerAId, friendship.PlayerBId);

        await _collection.InsertOneAsync(friendship, cancellationToken: ct);
    }

    public async Task DeleteAsync(Guid playerA, Guid playerB, CancellationToken ct = default)
    {
        var key = CanonicalKey(playerA, playerB);
        var filter = Builders<Friendship>.Filter.Eq(f => f.PlayerIdsKey, key);

        var result = await _collection.DeleteOneAsync(filter, ct);
        if (result.DeletedCount > 0)
            logger.LogInformation("Deleted friendship between {A} and {B}", playerA, playerB);
    }

    public async Task<long> CountForPlayerAsync(Guid playerId, CancellationToken ct = default)
    {
        var filter = Builders<Friendship>.Filter.Or(
            Builders<Friendship>.Filter.Eq(f => f.PlayerAId, playerId),
            Builders<Friendship>.Filter.Eq(f => f.PlayerBId, playerId));

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    private static string CanonicalKey(Guid x, Guid y)
    {
        var (a, b) = x.CompareTo(y) < 0 ? (x, y) : (y, x);
        return $"{a}:{b}";
    }
}
