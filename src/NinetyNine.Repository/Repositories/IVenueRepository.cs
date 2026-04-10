using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="Venue"/> documents.
/// </summary>
public interface IVenueRepository
{
    Task<Venue?> GetByIdAsync(Guid venueId, CancellationToken ct = default);
    Task<IReadOnlyList<Venue>> GetAllAsync(bool includePrivate, CancellationToken ct = default);
    Task CreateAsync(Venue venue, CancellationToken ct = default);
    Task UpdateAsync(Venue venue, CancellationToken ct = default);
    Task DeleteAsync(Guid venueId, CancellationToken ct = default);
}
