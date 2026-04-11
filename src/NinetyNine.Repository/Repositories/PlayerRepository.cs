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

    /// <summary>
    /// Looks up a player by email address using a case-insensitive match.
    /// Returns <c>null</c> if no player with that address exists.
    /// </summary>
    /// <param name="email">The email address to search for. Trimmed and lowercased before querying.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public async Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var normalized = email.Trim().ToLowerInvariant();
        var filter = Builders<Player>.Filter.Eq(p => p.EmailAddress, normalized);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Looks up a player by their current email verification token using an exact match.
    /// Returns <c>null</c> if the token is not found. The caller is responsible for expiry checks.
    /// </summary>
    /// <param name="token">The email verification token to look up.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public async Task<Player?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var filter = Builders<Player>.Filter.Eq(p => p.EmailVerificationToken, token);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Looks up a player by their current password reset token using an exact match.
    /// Returns <c>null</c> if the token is not found. The caller is responsible for expiry checks.
    /// </summary>
    /// <param name="token">The password reset token to look up.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public async Task<Player?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var filter = Builders<Player>.Filter.Eq(p => p.PasswordResetToken, token);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns <c>true</c> if a player with the given email address already exists (case-insensitive).
    /// Uses <c>CountDocumentsAsync</c> with <c>Limit = 1</c> for a server-side short-circuit.
    /// </summary>
    /// <param name="email">The email address to check. Trimmed and lowercased before querying.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var normalized = email.Trim().ToLowerInvariant();
        var filter = Builders<Player>.Filter.Eq(p => p.EmailAddress, normalized);
        var options = new CountOptions { Limit = 1 };
        var count = await _collection.CountDocumentsAsync(filter, options, ct);
        return count > 0;
    }

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

    public async Task<IReadOnlyList<Player>> ListAllAsync(CancellationToken ct = default)
    {
        var all = await _collection.Find(Builders<Player>.Filter.Empty).ToListAsync(ct);
        return all.AsReadOnly();
    }
}
