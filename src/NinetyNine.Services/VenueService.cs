using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

/// <summary>
/// Thin orchestration layer over <see cref="IVenueRepository"/> with basic validation.
/// </summary>
public sealed class VenueService(IVenueRepository venueRepository, ILogger<VenueService> logger)
    : IVenueService
{
    public async Task<Venue> CreateAsync(Venue venue, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(venue);

        if (string.IsNullOrWhiteSpace(venue.Name))
            throw new ArgumentException("Venue name is required.", nameof(venue));

        await venueRepository.CreateAsync(venue, ct);
        logger.LogInformation("Created venue {VenueId} '{VenueName}'", venue.VenueId, venue.Name);
        return venue;
    }

    public Task<Venue?> GetAsync(Guid venueId, CancellationToken ct = default)
        => venueRepository.GetByIdAsync(venueId, ct);

    public Task<IReadOnlyList<Venue>> ListAsync(bool includePrivate, CancellationToken ct = default)
        => venueRepository.GetAllAsync(includePrivate, ct);

    public async Task<Venue> UpdateAsync(Venue venue, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(venue);

        if (string.IsNullOrWhiteSpace(venue.Name))
            throw new ArgumentException("Venue name is required.", nameof(venue));

        await venueRepository.UpdateAsync(venue, ct);
        logger.LogDebug("Updated venue {VenueId}", venue.VenueId);
        return venue;
    }

    public async Task DeleteAsync(Guid venueId, CancellationToken ct = default)
    {
        await venueRepository.DeleteAsync(venueId, ct);
        logger.LogInformation("Deleted venue {VenueId}", venueId);
    }
}
