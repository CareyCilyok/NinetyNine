using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

/// <summary>
/// Thin orchestration layer over <see cref="IVenueRepository"/> with basic validation.
/// </summary>
public sealed class VenueService(
    IVenueRepository venueRepository,
    ICommunityRepository communityRepository,
    ICommunityMemberRepository communityMemberRepository,
    ILogger<VenueService> logger) : IVenueService
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

    public async Task<ServiceResult<Venue>> SetCommunityAffiliationAsync(
        Guid venueId,
        Guid? communityId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var venue = await venueRepository.GetByIdAsync(venueId, ct);
        if (venue is null)
            return ServiceResult<Venue>.Fail("VenueNotFound", "Venue not found.");

        // Authorization: only the venue's creator can set affiliation.
        // Legacy venues seeded before Sprint 3 have a null CreatedByPlayerId
        // — the first editor claims ownership by passing their player id,
        // and we stamp the field on first save.
        if (venue.CreatedByPlayerId is null)
        {
            venue.CreatedByPlayerId = byPlayerId;
            logger.LogInformation(
                "Claiming legacy venue {VenueId} for player {PlayerId}", venueId, byPlayerId);
        }
        else if (venue.CreatedByPlayerId != byPlayerId)
        {
            return ServiceResult<Venue>.Fail(
                "NotAuthorized",
                "Only the pool player who created this venue can change its community affiliation.");
        }

        // When clearing, just null out and persist.
        if (communityId is null)
        {
            venue.CommunityId = null;
            await venueRepository.UpdateAsync(venue, ct);
            logger.LogInformation(
                "Cleared community affiliation on venue {VenueId}", venueId);
            return ServiceResult<Venue>.Ok(venue);
        }

        // When affiliating, the community must exist and the actor must
        // be a current member of it (so random pool players can't steer
        // a venue into a community they have no stake in).
        var community = await communityRepository.GetByIdAsync(communityId.Value, ct);
        if (community is null)
            return ServiceResult<Venue>.Fail("CommunityNotFound", "Community not found.");

        var membership = await communityMemberRepository.GetMembershipAsync(
            communityId.Value, byPlayerId, ct);
        if (membership is null)
            return ServiceResult<Venue>.Fail(
                "NotACommunityMember",
                "You must be a member of the community to affiliate a venue with it.");

        venue.CommunityId = communityId;
        await venueRepository.UpdateAsync(venue, ct);

        logger.LogInformation(
            "Affiliated venue {VenueId} with community {CommunityId} by player {PlayerId}",
            venueId, communityId, byPlayerId);

        return ServiceResult<Venue>.Ok(venue);
    }
}
