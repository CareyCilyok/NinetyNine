using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="IPlayerRepository"/>.
/// </summary>
public sealed class PlayerRepository(INinetyNineDbContext context, ILogger<PlayerRepository> logger)
    : IPlayerRepository
{
    private readonly IMongoCollection<Player> _collection = context.Players;

    public async Task<Player?> GetByIdAsync(Guid playerId, CancellationToken ct = default)
    {
        logger.LogDebug("Getting player by ID {PlayerId}", playerId);
        var filter = Builders<Player>.Filter.Eq(p => p.PlayerId, playerId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Player?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var filter = Builders<Player>.Filter.Eq(p => p.DisplayName, displayName);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var filter = Builders<Player>.Filter.Eq(p => p.DisplayName, displayName);
        return await _collection.Find(filter).AnyAsync(ct);
    }

    public async Task<IReadOnlyList<Player>> SearchAsync(
        string query, int limit, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        // Case-insensitive prefix search on DisplayName
        var filter = Builders<Player>.Filter.Regex(
            p => p.DisplayName,
            new MongoDB.Bson.BsonRegularExpression(query, "i"));

        var results = await _collection.Find(filter)
            .Limit(limit)
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    public async Task CreateAsync(Player player, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        logger.LogInformation("Creating player {PlayerId} with DisplayName {DisplayName}",
            player.PlayerId, player.DisplayName);
        await _collection.InsertOneAsync(player, cancellationToken: ct);
    }

    public async Task UpdateAsync(Player player, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        logger.LogDebug("Updating player {PlayerId}", player.PlayerId);
        var filter = Builders<Player>.Filter.Eq(p => p.PlayerId, player.PlayerId);
        var result = await _collection.ReplaceOneAsync(filter, player, cancellationToken: ct);

        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Player {player.PlayerId} not found.");
    }

    public async Task DeleteAsync(Guid playerId, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting player {PlayerId}", playerId);
        var filter = Builders<Player>.Filter.Eq(p => p.PlayerId, playerId);
        await _collection.DeleteOneAsync(filter, cancellationToken: ct);
    }
}
