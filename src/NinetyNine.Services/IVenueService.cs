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

    /// <summary>
    /// Set or clear the venue's community affiliation (<see cref="Venue.CommunityId"/>).
    /// Authorization: the acting pool player must be the venue's
    /// <see cref="Venue.CreatedByPlayerId"/>. For legacy venues with a null
    /// <c>CreatedByPlayerId</c> (seeded before Sprint 3), the first editor
    /// claims authorship by passing their player id as the actor — the
    /// service stamps the field on first save.
    /// <para>
    /// <paramref name="communityId"/> must be either <c>null</c> (to clear)
    /// or the id of an existing community the actor is currently a member
    /// of. Venue ↔ community is affiliation-only; see the
    /// <c>project-pool-players-only</c> memory and the plan's 2026-04-11
    /// fork-B reversal.
    /// </para>
    /// </summary>
    /// <returns>
    /// A <see cref="ServiceResult{T}"/> carrying the updated venue on success,
    /// or a domain-coded failure (<c>VenueNotFound</c>, <c>NotAuthorized</c>,
    /// <c>CommunityNotFound</c>, <c>NotACommunityMember</c>).
    /// </returns>
    Task<ServiceResult<Venue>> SetCommunityAffiliationAsync(
        Guid venueId,
        Guid? communityId,
        Guid byPlayerId,
        CancellationToken ct = default);
}
