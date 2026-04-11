using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="ICommunityJoinRequestRepository"/>.
/// </summary>
public sealed class CommunityJoinRequestRepository(
    INinetyNineDbContext context,
    ILogger<CommunityJoinRequestRepository> logger) : ICommunityJoinRequestRepository
{
    private readonly IMongoCollection<CommunityJoinRequest> _collection = context.CommunityJoinRequests;

    public async Task<CommunityJoinRequest?> GetByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var filter = Builders<CommunityJoinRequest>.Filter.Eq(r => r.RequestId, requestId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<CommunityJoinRequest?> GetPendingAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityJoinRequest>.Filter.And(
            Builders<CommunityJoinRequest>.Filter.Eq(r => r.CommunityId, communityId),
            Builders<CommunityJoinRequest>.Filter.Eq(r => r.PlayerId, playerId),
            Builders<CommunityJoinRequest>.Filter.Eq(r => r.Status, CommunityJoinRequestStatus.Pending));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CommunityJoinRequest>> ListPendingByCommunityAsync(
        Guid communityId,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityJoinRequest>.Filter.And(
            Builders<CommunityJoinRequest>.Filter.Eq(r => r.CommunityId, communityId),
            Builders<CommunityJoinRequest>.Filter.Eq(r => r.Status, CommunityJoinRequestStatus.Pending));

        var results = await _collection.Find(filter)
            .SortBy(r => r.CreatedAt)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<CommunityJoinRequest>> ListPendingByPlayerAsync(
        Guid playerId,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityJoinRequest>.Filter.And(
            Builders<CommunityJoinRequest>.Filter.Eq(r => r.PlayerId, playerId),
            Builders<CommunityJoinRequest>.Filter.Eq(r => r.Status, CommunityJoinRequestStatus.Pending));

        var results = await _collection.Find(filter)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task CreateAsync(CommunityJoinRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogInformation(
            "Creating community join request {RequestId} by {Player} to {Community}",
            request.RequestId, request.PlayerId, request.CommunityId);
        await _collection.InsertOneAsync(request, cancellationToken: ct);
    }

    public async Task UpdateStatusAsync(
        Guid requestId,
        CommunityJoinRequestStatus status,
        DateTime decidedAt,
        Guid? decidedByPlayerId,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityJoinRequest>.Filter.Eq(r => r.RequestId, requestId);
        var update = Builders<CommunityJoinRequest>.Update
            .Set(r => r.Status, status)
            .Set(r => r.DecidedAt, decidedAt)
            .Set(r => r.DecidedByPlayerId, decidedByPlayerId);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Community join request {requestId} not found.");
    }

    public async Task<long> SweepExpiredAsync(DateTime olderThan, CancellationToken ct = default)
    {
        var filter = Builders<CommunityJoinRequest>.Filter.And(
            Builders<CommunityJoinRequest>.Filter.Eq(r => r.Status, CommunityJoinRequestStatus.Pending),
            Builders<CommunityJoinRequest>.Filter.Lt(r => r.CreatedAt, olderThan));

        var update = Builders<CommunityJoinRequest>.Update
            .Set(r => r.Status, CommunityJoinRequestStatus.Expired)
            .Set(r => r.DecidedAt, DateTime.UtcNow);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        if (result.ModifiedCount > 0)
            logger.LogInformation("Expired {Count} community join request(s)", result.ModifiedCount);

        return result.ModifiedCount;
    }
}
