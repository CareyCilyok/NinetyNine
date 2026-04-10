using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Manages CRUD operations for venues.
/// </summary>
public interface IVenueService
{
    Task<Venue> CreateAsync(Venue venue, CancellationToken ct = default);
    Task<Venue?> GetAsync(Guid venueId, CancellationToken ct = default);
    Task<IReadOnlyList<Venue>> ListAsync(bool includePrivate, CancellationToken ct = default);
    Task<Venue> UpdateAsync(Venue venue, CancellationToken ct = default);
    Task DeleteAsync(Guid venueId, CancellationToken ct = default);
}
