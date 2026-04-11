using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="ICommunityMemberRepository"/>.
/// </summary>
public sealed class CommunityMemberRepository(
    INinetyNineDbContext context,
    ILogger<CommunityMemberRepository> logger) : ICommunityMemberRepository
{
    private readonly IMongoCollection<CommunityMembership> _collection = context.CommunityMembers;

    public async Task<CommunityMembership?> GetMembershipAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityMembership>.Filter.And(
            Builders<CommunityMembership>.Filter.Eq(m => m.CommunityId, communityId),
            Builders<CommunityMembership>.Filter.Eq(m => m.PlayerId, playerId));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CommunityMembership>> ListMembersAsync(
        Guid communityId,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityMembership>.Filter.Eq(m => m.CommunityId, communityId);
        var results = await _collection.Find(filter)
            .SortBy(m => m.JoinedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<CommunityMembership>> ListCommunitiesForPlayerAsync(
        Guid playerId,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityMembership>.Filter.Eq(m => m.PlayerId, playerId);
        var results = await _collection.Find(filter).ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task AddAsync(CommunityMembership membership, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(membership);
        logger.LogInformation(
            "Adding membership {PlayerId} → {CommunityId} as {Role}",
            membership.PlayerId, membership.CommunityId, membership.Role);
        await _collection.InsertOneAsync(membership, cancellationToken: ct);
    }

    public async Task RemoveAsync(Guid communityId, Guid playerId, CancellationToken ct = default)
    {
        var filter = Builders<CommunityMembership>.Filter.And(
            Builders<CommunityMembership>.Filter.Eq(m => m.CommunityId, communityId),
            Builders<CommunityMembership>.Filter.Eq(m => m.PlayerId, playerId));

        var result = await _collection.DeleteOneAsync(filter, ct);
        if (result.DeletedCount > 0)
            logger.LogInformation(
                "Removed membership {PlayerId} from {CommunityId}", playerId, communityId);
    }

    public async Task<long> CountMembersAsync(Guid communityId, CancellationToken ct = default)
    {
        var filter = Builders<CommunityMembership>.Filter.Eq(m => m.CommunityId, communityId);
        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task<long> RemoveAllFromCommunityAsync(Guid communityId, CancellationToken ct = default)
    {
        var filter = Builders<CommunityMembership>.Filter.Eq(m => m.CommunityId, communityId);
        var result = await _collection.DeleteManyAsync(filter, ct);
        if (result.DeletedCount > 0)
            logger.LogInformation(
                "Removed {Count} membership(s) from community {CommunityId}",
                result.DeletedCount, communityId);
        return result.DeletedCount;
    }
}
