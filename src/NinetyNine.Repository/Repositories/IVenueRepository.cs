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

    /// <summary>
    /// Bulk-clears <see cref="Venue.CommunityId"/> on every venue currently
    /// affiliated with the given community. Used by
    /// <c>CommunityService.DeleteAsync</c> as part of the cascade when a
    /// community is deleted. Returns the number of venues updated.
    /// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 2 S2.1.</para>
    /// </summary>
    Task<long> ClearCommunityAffiliationsAsync(Guid communityId, CancellationToken ct = default);
}
