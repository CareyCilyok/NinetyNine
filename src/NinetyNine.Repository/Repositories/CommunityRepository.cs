using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="ICommunityRepository"/>.
/// </summary>
public sealed class CommunityRepository(
    INinetyNineDbContext context,
    ILogger<CommunityRepository> logger) : ICommunityRepository
{
    private static readonly Collation CaseInsensitive =
        new("en", strength: CollationStrength.Secondary);

    private readonly IMongoCollection<Community> _collection = context.Communities;

    public async Task<Community?> GetByIdAsync(Guid communityId, CancellationToken ct = default)
    {
        var filter = Builders<Community>.Filter.Eq(c => c.CommunityId, communityId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Community?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var filter = Builders<Community>.Filter.Eq(c => c.Slug, slug);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Community?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        // Use the collation-strength-2 index for case-insensitive match.
        var filter = Builders<Community>.Filter.Eq(c => c.Name, name);
        var options = new FindOptions { Collation = CaseInsensitive };
        return await _collection.Find(filter, options).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Community>> ListByOwnerPlayerAsync(Guid ownerPlayerId, CancellationToken ct = default)
    {
        var filter = Builders<Community>.Filter.Eq(c => c.OwnerPlayerId, ownerPlayerId);
        var results = await _collection.Find(filter)
            .SortBy(c => c.Name)
            .ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<Community>> SearchPublicByNameAsync(
        string namePrefix,
        int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(namePrefix))
            return Array.Empty<Community>();

        // Escape regex metacharacters from user input, then case-insensitive
        // prefix match. Only Public communities — private communities must
        // never surface through discovery.
        var escaped = System.Text.RegularExpressions.Regex.Escape(namePrefix);
        var filter = Builders<Community>.Filter.And(
            Builders<Community>.Filter.Eq(c => c.Visibility, CommunityVisibility.Public),
            Builders<Community>.Filter.Regex(
                c => c.Name,
                new MongoDB.Bson.BsonRegularExpression($"^{escaped}", "i")));

        var results = await _collection.Find(filter)
            .SortBy(c => c.Name)
            .Limit(limit)
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    public async Task CreateAsync(Community community, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(community);
        logger.LogInformation(
            "Creating community {CommunityId} '{Name}' (owner {OwnerPlayerId})",
            community.CommunityId, community.Name, community.OwnerPlayerId);
        await _collection.InsertOneAsync(community, cancellationToken: ct);
    }

    public async Task UpdateAsync(Community community, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(community);

        var filter = Builders<Community>.Filter.Eq(c => c.CommunityId, community.CommunityId);
        var result = await _collection.ReplaceOneAsync(filter, community, cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Community {community.CommunityId} not found.");
    }

    public async Task DeleteAsync(Guid communityId, CancellationToken ct = default)
    {
        var filter = Builders<Community>.Filter.Eq(c => c.CommunityId, communityId);
        await _collection.DeleteOneAsync(filter, ct);
        logger.LogInformation("Deleted community {CommunityId}", communityId);
    }
}
