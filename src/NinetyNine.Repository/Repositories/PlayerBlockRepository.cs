using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

public sealed class PlayerBlockRepository(
    INinetyNineDbContext context,
    ILogger<PlayerBlockRepository> logger) : IPlayerBlockRepository
{
    private readonly IMongoCollection<PlayerBlock> _collection =
        context.PlayerBlocks;

    public async Task<PlayerBlock?> GetBlockAsync(
        Guid playerA, Guid playerB, CancellationToken ct = default)
    {
        var filter = Builders<PlayerBlock>.Filter.Or(
            Builders<PlayerBlock>.Filter.And(
                Builders<PlayerBlock>.Filter.Eq(b => b.BlockerPlayerId, playerA),
                Builders<PlayerBlock>.Filter.Eq(b => b.BlockedPlayerId, playerB)),
            Builders<PlayerBlock>.Filter.And(
                Builders<PlayerBlock>.Filter.Eq(b => b.BlockerPlayerId, playerB),
                Builders<PlayerBlock>.Filter.Eq(b => b.BlockedPlayerId, playerA)));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListBlockedIdsAsync(
        Guid playerId, CancellationToken ct = default)
    {
        var filter = Builders<PlayerBlock>.Filter.Or(
            Builders<PlayerBlock>.Filter.Eq(b => b.BlockerPlayerId, playerId),
            Builders<PlayerBlock>.Filter.Eq(b => b.BlockedPlayerId, playerId));

        var blocks = await _collection.Find(filter).ToListAsync(ct);
        var ids = blocks.Select(b =>
            b.BlockerPlayerId == playerId ? b.BlockedPlayerId : b.BlockerPlayerId)
            .Distinct()
            .ToList();
        return ids.AsReadOnly();
    }

    public async Task CreateAsync(PlayerBlock block, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(block);
        await _collection.InsertOneAsync(block, cancellationToken: ct);
        logger.LogInformation(
            "Player {Blocker} blocked {Blocked}",
            block.BlockerPlayerId, block.BlockedPlayerId);
    }

    public async Task DeleteAsync(Guid blockId, CancellationToken ct = default)
    {
        var filter = Builders<PlayerBlock>.Filter.Eq(b => b.BlockId, blockId);
        await _collection.DeleteOneAsync(filter, ct);
    }
}
