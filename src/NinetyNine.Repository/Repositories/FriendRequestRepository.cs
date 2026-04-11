using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="IFriendRequestRepository"/>.
/// </summary>
public sealed class FriendRequestRepository(
    INinetyNineDbContext context,
    ILogger<FriendRequestRepository> logger) : IFriendRequestRepository
{
    private readonly IMongoCollection<FriendRequest> _collection = context.FriendRequests;

    public async Task<FriendRequest?> GetByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var filter = Builders<FriendRequest>.Filter.Eq(r => r.RequestId, requestId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<FriendRequest?> GetPendingAsync(Guid fromPlayerId, Guid toPlayerId, CancellationToken ct = default)
    {
        var filter = Builders<FriendRequest>.Filter.And(
            Builders<FriendRequest>.Filter.Eq(r => r.FromPlayerId, fromPlayerId),
            Builders<FriendRequest>.Filter.Eq(r => r.ToPlayerId, toPlayerId),
            Builders<FriendRequest>.Filter.Eq(r => r.Status, FriendRequestStatus.Pending));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<FriendRequest>> ListIncomingAsync(
        Guid playerId,
        FriendRequestStatus? status = null,
        CancellationToken ct = default)
    {
        var filter = Builders<FriendRequest>.Filter.Eq(r => r.ToPlayerId, playerId);
        if (status.HasValue)
        {
            filter = Builders<FriendRequest>.Filter.And(
                filter,
                Builders<FriendRequest>.Filter.Eq(r => r.Status, status.Value));
        }

        var results = await _collection.Find(filter)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<FriendRequest>> ListOutgoingAsync(
        Guid playerId,
        FriendRequestStatus? status = null,
        CancellationToken ct = default)
    {
        var filter = Builders<FriendRequest>.Filter.Eq(r => r.FromPlayerId, playerId);
        if (status.HasValue)
        {
            filter = Builders<FriendRequest>.Filter.And(
                filter,
                Builders<FriendRequest>.Filter.Eq(r => r.Status, status.Value));
        }

        var results = await _collection.Find(filter)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task CreateAsync(FriendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogInformation(
            "Creating friend request {RequestId} from {From} to {To}",
            request.RequestId, request.FromPlayerId, request.ToPlayerId);
        await _collection.InsertOneAsync(request, cancellationToken: ct);
    }

    public async Task UpdateStatusAsync(
        Guid requestId,
        FriendRequestStatus status,
        DateTime respondedAt,
        CancellationToken ct = default)
    {
        var filter = Builders<FriendRequest>.Filter.Eq(r => r.RequestId, requestId);
        var update = Builders<FriendRequest>.Update
            .Set(r => r.Status, status)
            .Set(r => r.RespondedAt, respondedAt);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Friend request {requestId} not found.");

        logger.LogInformation("Friend request {RequestId} updated to {Status}", requestId, status);
    }

    public async Task<long> SweepExpiredAsync(DateTime olderThan, CancellationToken ct = default)
    {
        var filter = Builders<FriendRequest>.Filter.And(
            Builders<FriendRequest>.Filter.Eq(r => r.Status, FriendRequestStatus.Pending),
            Builders<FriendRequest>.Filter.Lt(r => r.CreatedAt, olderThan));

        var update = Builders<FriendRequest>.Update
            .Set(r => r.Status, FriendRequestStatus.Expired)
            .Set(r => r.RespondedAt, DateTime.UtcNow);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        if (result.ModifiedCount > 0)
            logger.LogInformation("Expired {Count} pending friend request(s)", result.ModifiedCount);

        return result.ModifiedCount;
    }
}
