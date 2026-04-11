using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="ICommunityInvitationRepository"/>.
/// </summary>
public sealed class CommunityInvitationRepository(
    INinetyNineDbContext context,
    ILogger<CommunityInvitationRepository> logger) : ICommunityInvitationRepository
{
    private readonly IMongoCollection<CommunityInvitation> _collection = context.CommunityInvitations;

    public async Task<CommunityInvitation?> GetByIdAsync(Guid invitationId, CancellationToken ct = default)
    {
        var filter = Builders<CommunityInvitation>.Filter.Eq(i => i.InvitationId, invitationId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<CommunityInvitation?> GetPendingAsync(
        Guid communityId,
        Guid invitedPlayerId,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityInvitation>.Filter.And(
            Builders<CommunityInvitation>.Filter.Eq(i => i.CommunityId, communityId),
            Builders<CommunityInvitation>.Filter.Eq(i => i.InvitedPlayerId, invitedPlayerId),
            Builders<CommunityInvitation>.Filter.Eq(i => i.Status, CommunityInvitationStatus.Pending));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CommunityInvitation>> ListByInviteeAsync(
        Guid invitedPlayerId,
        CommunityInvitationStatus? status = null,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityInvitation>.Filter.Eq(i => i.InvitedPlayerId, invitedPlayerId);
        if (status.HasValue)
        {
            filter = Builders<CommunityInvitation>.Filter.And(
                filter,
                Builders<CommunityInvitation>.Filter.Eq(i => i.Status, status.Value));
        }

        var results = await _collection.Find(filter)
            .SortByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task<long> CountSentByInviterToTargetAsync(
        Guid inviterPlayerId,
        Guid invitedPlayerId,
        DateTime sinceUtc,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityInvitation>.Filter.And(
            Builders<CommunityInvitation>.Filter.Eq(i => i.InvitedByPlayerId, inviterPlayerId),
            Builders<CommunityInvitation>.Filter.Eq(i => i.InvitedPlayerId, invitedPlayerId),
            Builders<CommunityInvitation>.Filter.Gte(i => i.CreatedAt, sinceUtc));

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<CommunityInvitation>> ListByCommunityAsync(
        Guid communityId,
        CommunityInvitationStatus? status = null,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityInvitation>.Filter.Eq(i => i.CommunityId, communityId);
        if (status.HasValue)
        {
            filter = Builders<CommunityInvitation>.Filter.And(
                filter,
                Builders<CommunityInvitation>.Filter.Eq(i => i.Status, status.Value));
        }

        var results = await _collection.Find(filter)
            .SortByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task CreateAsync(CommunityInvitation invitation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        logger.LogInformation(
            "Creating community invitation {InvitationId} to player {Target} for community {Community}",
            invitation.InvitationId, invitation.InvitedPlayerId, invitation.CommunityId);
        await _collection.InsertOneAsync(invitation, cancellationToken: ct);
    }

    public async Task UpdateStatusAsync(
        Guid invitationId,
        CommunityInvitationStatus status,
        DateTime respondedAt,
        CancellationToken ct = default)
    {
        var filter = Builders<CommunityInvitation>.Filter.Eq(i => i.InvitationId, invitationId);
        var update = Builders<CommunityInvitation>.Update
            .Set(i => i.Status, status)
            .Set(i => i.RespondedAt, respondedAt);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Community invitation {invitationId} not found.");
    }

    public async Task<long> SweepExpiredAsync(DateTime olderThan, CancellationToken ct = default)
    {
        var filter = Builders<CommunityInvitation>.Filter.And(
            Builders<CommunityInvitation>.Filter.Eq(i => i.Status, CommunityInvitationStatus.Pending),
            Builders<CommunityInvitation>.Filter.Lt(i => i.CreatedAt, olderThan));

        var update = Builders<CommunityInvitation>.Update
            .Set(i => i.Status, CommunityInvitationStatus.Expired)
            .Set(i => i.RespondedAt, DateTime.UtcNow);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        if (result.ModifiedCount > 0)
            logger.LogInformation("Expired {Count} community invitation(s)", result.ModifiedCount);

        return result.ModifiedCount;
    }
}
