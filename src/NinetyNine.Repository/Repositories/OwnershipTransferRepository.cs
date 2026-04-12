using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public sealed class OwnershipTransferRepository(
    INinetyNineDbContext context,
    ILogger<OwnershipTransferRepository> logger) : IOwnershipTransferRepository
{
    private readonly IMongoCollection<OwnershipTransfer> _collection =
        context.OwnershipTransfers;

    public async Task<OwnershipTransfer?> GetByIdAsync(
        Guid transferId, CancellationToken ct = default)
    {
        var filter = Builders<OwnershipTransfer>.Filter.Eq(t => t.TransferId, transferId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<OwnershipTransfer?> GetPendingByCommunityAsync(
        Guid communityId, CancellationToken ct = default)
    {
        var filter = Builders<OwnershipTransfer>.Filter.And(
            Builders<OwnershipTransfer>.Filter.Eq(t => t.CommunityId, communityId),
            Builders<OwnershipTransfer>.Filter.Eq(t => t.Status, OwnershipTransferStatus.Pending));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<OwnershipTransfer>> ListPendingForTargetAsync(
        Guid toPlayerId, CancellationToken ct = default)
    {
        var filter = Builders<OwnershipTransfer>.Filter.And(
            Builders<OwnershipTransfer>.Filter.Eq(t => t.ToPlayerId, toPlayerId),
            Builders<OwnershipTransfer>.Filter.Eq(t => t.Status, OwnershipTransferStatus.Pending));
        var results = await _collection.Find(filter).ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task CreateAsync(OwnershipTransfer transfer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        await _collection.InsertOneAsync(transfer, cancellationToken: ct);
        logger.LogInformation(
            "Created ownership transfer {TransferId}: community {CommunityId}, {From} → {To}",
            transfer.TransferId, transfer.CommunityId, transfer.FromPlayerId, transfer.ToPlayerId);
    }

    public async Task UpdateAsync(OwnershipTransfer transfer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        var filter = Builders<OwnershipTransfer>.Filter.Eq(t => t.TransferId, transfer.TransferId);
        await _collection.ReplaceOneAsync(filter, transfer, cancellationToken: ct);
    }
}
