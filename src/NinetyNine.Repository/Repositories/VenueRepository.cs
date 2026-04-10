using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// MongoDB-backed implementation of <see cref="IVenueRepository"/>.
/// </summary>
public sealed class VenueRepository(INinetyNineDbContext context, ILogger<VenueRepository> logger)
    : IVenueRepository
{
    private readonly IMongoCollection<Venue> _collection = context.Venues;

    public async Task<Venue?> GetByIdAsync(Guid venueId, CancellationToken ct = default)
    {
        logger.LogDebug("Getting venue by ID {VenueId}", venueId);
        var filter = Builders<Venue>.Filter.Eq(v => v.VenueId, venueId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Venue>> GetAllAsync(bool includePrivate, CancellationToken ct = default)
    {
        var filter = includePrivate
            ? Builders<Venue>.Filter.Empty
            : Builders<Venue>.Filter.Eq(v => v.Private, false);

        var results = await _collection.Find(filter)
            .SortBy(v => v.Name)
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    public async Task CreateAsync(Venue venue, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(venue);
        logger.LogInformation("Creating venue {VenueId} '{VenueName}'", venue.VenueId, venue.Name);
        await _collection.InsertOneAsync(venue, cancellationToken: ct);
    }

    public async Task UpdateAsync(Venue venue, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(venue);
        logger.LogDebug("Updating venue {VenueId}", venue.VenueId);
        var filter = Builders<Venue>.Filter.Eq(v => v.VenueId, venue.VenueId);
        var result = await _collection.ReplaceOneAsync(filter, venue, cancellationToken: ct);

        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Venue {venue.VenueId} not found.");
    }

    public async Task DeleteAsync(Guid venueId, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting venue {VenueId}", venueId);
        var filter = Builders<Venue>.Filter.Eq(v => v.VenueId, venueId);
        await _collection.DeleteOneAsync(filter, cancellationToken: ct);
    }
}
