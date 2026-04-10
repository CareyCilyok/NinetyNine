using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="IGameRepository"/>.
/// </summary>
public sealed class GameRepository(INinetyNineDbContext context, ILogger<GameRepository> logger)
    : IGameRepository
{
    private readonly IMongoCollection<Game> _collection = context.Games;

    public async Task<Game?> GetByIdAsync(Guid gameId, CancellationToken ct = default)
    {
        logger.LogDebug("Getting game by ID {GameId}", gameId);
        var filter = Builders<Game>.Filter.Eq(g => g.GameId, gameId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetByPlayerAsync(
        Guid playerId, int skip, int limit, CancellationToken ct = default)
    {
        var filter = Builders<Game>.Filter.Eq(g => g.PlayerId, playerId);
        var results = await _collection.Find(filter)
            .SortByDescending(g => g.WhenPlayed)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<Game>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        var results = await _collection.Find(Builders<Game>.Filter.Empty)
            .SortByDescending(g => g.WhenPlayed)
            .Limit(limit)
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<Game>> GetCompletedByPlayerAsync(
        Guid playerId, CancellationToken ct = default)
    {
        var filter = Builders<Game>.Filter.And(
            Builders<Game>.Filter.Eq(g => g.PlayerId, playerId),
            Builders<Game>.Filter.Eq(g => g.GameState, GameState.Completed));

        var results = await _collection.Find(filter)
            .SortByDescending(g => g.WhenPlayed)
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    public async Task<Game?> GetActiveForPlayerAsync(Guid playerId, CancellationToken ct = default)
    {
        var filter = Builders<Game>.Filter.And(
            Builders<Game>.Filter.Eq(g => g.PlayerId, playerId),
            Builders<Game>.Filter.Eq(g => g.GameState, GameState.InProgress));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task CreateAsync(Game game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        logger.LogInformation("Creating game {GameId} for player {PlayerId}", game.GameId, game.PlayerId);
        await _collection.InsertOneAsync(game, cancellationToken: ct);
    }

    public async Task UpdateAsync(Game game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        logger.LogDebug("Updating game {GameId}", game.GameId);
        var filter = Builders<Game>.Filter.Eq(g => g.GameId, game.GameId);
        var result = await _collection.ReplaceOneAsync(filter, game, cancellationToken: ct);

        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Game {game.GameId} not found.");
    }

    public async Task DeleteAsync(Guid gameId, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting game {GameId}", gameId);
        var filter = Builders<Game>.Filter.Eq(g => g.GameId, gameId);
        await _collection.DeleteOneAsync(filter, cancellationToken: ct);
    }
}
