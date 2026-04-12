using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Integration tests for <see cref="IVenueService.SetCommunityAffiliationAsync"/>.
/// Exercises every branch of the authorization and validation rules
/// locked in Sprint 3 S3.1.
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class VenueServiceAffiliationTests(MongoFixture fixture)
{
    private (IVenueService Service,
             IPlayerRepository Players,
             IVenueRepository Venues,
             ICommunityService CommunityService)
        CreateService()
    {
        var ctx = fixture.CreateDbContext();
        var players = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var venues = new VenueRepository(ctx, NullLogger<VenueRepository>.Instance);
        var communities = new CommunityRepository(ctx, NullLogger<CommunityRepository>.Instance);
        var members = new CommunityMemberRepository(ctx, NullLogger<CommunityMemberRepository>.Instance);
        var invites = new CommunityInvitationRepository(ctx, NullLogger<CommunityInvitationRepository>.Instance);
        var joins = new CommunityJoinRequestRepository(ctx, NullLogger<CommunityJoinRequestRepository>.Instance);

        var venueService = new VenueService(
            venues, communities, members,
            NullLogger<VenueService>.Instance);

        var xfers = new OwnershipTransferRepository(ctx, NullLogger<OwnershipTransferRepository>.Instance);
        var communityService = new CommunityService(
            communities, members, invites, joins, players, venues, xfers,
            NullLogger<CommunityService>.Instance);

        return (venueService, players, venues, communityService);
    }

    private static async Task<Player> CreatePlayer(IPlayerRepository repo)
    {
        var p = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "p_" + Guid.NewGuid().ToString("N")[..12],
            EmailAddress = Guid.NewGuid().ToString("N") + "@example.local",
            EmailVerified = true,
            SchemaVersion = 2,
        };
        await repo.CreateAsync(p);
        return p;
    }

    private static async Task<Venue> CreateVenue(
        IVenueRepository repo,
        Guid? createdByPlayerId = null)
    {
        var v = new Venue
        {
            VenueId = Guid.NewGuid(),
            Name = "Venue " + Guid.NewGuid().ToString("N")[..8],
            CreatedByPlayerId = createdByPlayerId,
        };
        await repo.CreateAsync(v);
        return v;
    }

    private static async Task<Community> CreatePlayerOwnedCommunity(
        ICommunityService svc,
        Guid ownerPlayerId,
        string name) =>
        (await svc.CreatePlayerOwnedAsync(
            ownerPlayerId,
            name,
            name.ToLowerInvariant().Replace(' ', '-') + "-" + Guid.NewGuid().ToString("N")[..8],
            null,
            CommunityVisibility.Public)).Value!;

    [Fact]
    public async Task SetAffiliation_Succeeds_WhenActorIsCreatorAndCommunityMember()
    {
        var (svc, players, venues, communities) = CreateService();
        var owner = await CreatePlayer(players);
        var community = await CreatePlayerOwnedCommunity(communities, owner.PlayerId, "Regulars");
        var venue = await CreateVenue(venues, createdByPlayerId: owner.PlayerId);

        var result = await svc.SetCommunityAffiliationAsync(
            venue.VenueId, community.CommunityId, owner.PlayerId);

        result.Success.Should().BeTrue();
        result.Value!.CommunityId.Should().Be(community.CommunityId);

        // Verify persistence.
        (await venues.GetByIdAsync(venue.VenueId))!.CommunityId
            .Should().Be(community.CommunityId);
    }

    [Fact]
    public async Task SetAffiliation_RejectsNonCreator()
    {
        var (svc, players, venues, communities) = CreateService();
        var creator = await CreatePlayer(players);
        var imposter = await CreatePlayer(players);
        var community = await CreatePlayerOwnedCommunity(communities, imposter.PlayerId, "Imposter's Club");
        var venue = await CreateVenue(venues, createdByPlayerId: creator.PlayerId);

        var result = await svc.SetCommunityAffiliationAsync(
            venue.VenueId, community.CommunityId, imposter.PlayerId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NotAuthorized");
    }

    [Fact]
    public async Task SetAffiliation_RejectsNonMemberOfTargetCommunity()
    {
        var (svc, players, venues, communities) = CreateService();
        var creator = await CreatePlayer(players);
        var otherOwner = await CreatePlayer(players);
        var community = await CreatePlayerOwnedCommunity(communities, otherOwner.PlayerId, "Other's Club");
        var venue = await CreateVenue(venues, createdByPlayerId: creator.PlayerId);

        // creator is the venue's author but is NOT a member of that community.
        var result = await svc.SetCommunityAffiliationAsync(
            venue.VenueId, community.CommunityId, creator.PlayerId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NotACommunityMember");
    }

    [Fact]
    public async Task SetAffiliation_RejectsMissingCommunity()
    {
        var (svc, players, venues, _) = CreateService();
        var creator = await CreatePlayer(players);
        var venue = await CreateVenue(venues, createdByPlayerId: creator.PlayerId);

        var result = await svc.SetCommunityAffiliationAsync(
            venue.VenueId, Guid.NewGuid(), creator.PlayerId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CommunityNotFound");
    }

    [Fact]
    public async Task SetAffiliation_RejectsMissingVenue()
    {
        var (svc, players, _, communities) = CreateService();
        var owner = await CreatePlayer(players);
        var community = await CreatePlayerOwnedCommunity(communities, owner.PlayerId, "Orphan");

        var result = await svc.SetCommunityAffiliationAsync(
            Guid.NewGuid(), community.CommunityId, owner.PlayerId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VenueNotFound");
    }

    [Fact]
    public async Task SetAffiliation_ClearsWithNullCommunityId()
    {
        var (svc, players, venues, communities) = CreateService();
        var owner = await CreatePlayer(players);
        var community = await CreatePlayerOwnedCommunity(communities, owner.PlayerId, "To Clear");
        var venue = await CreateVenue(venues, createdByPlayerId: owner.PlayerId);

        // Affiliate first.
        await svc.SetCommunityAffiliationAsync(venue.VenueId, community.CommunityId, owner.PlayerId);

        // Then clear.
        var clearResult = await svc.SetCommunityAffiliationAsync(
            venue.VenueId, null, owner.PlayerId);

        clearResult.Success.Should().BeTrue();
        (await venues.GetByIdAsync(venue.VenueId))!.CommunityId.Should().BeNull();
    }

    [Fact]
    public async Task SetAffiliation_LegacyVenue_FirstEditorClaimsAuthorship()
    {
        var (svc, players, venues, communities) = CreateService();
        var firstEditor = await CreatePlayer(players);
        var community = await CreatePlayerOwnedCommunity(
            communities, firstEditor.PlayerId, "First-Editor's Club");

        // Legacy venue: no CreatedByPlayerId.
        var legacyVenue = await CreateVenue(venues, createdByPlayerId: null);

        var result = await svc.SetCommunityAffiliationAsync(
            legacyVenue.VenueId, community.CommunityId, firstEditor.PlayerId);

        result.Success.Should().BeTrue();

        var reloaded = await venues.GetByIdAsync(legacyVenue.VenueId);
        reloaded!.CreatedByPlayerId.Should().Be(firstEditor.PlayerId,
            "the first editor's claim should stamp the legacy field");
        reloaded.CommunityId.Should().Be(community.CommunityId);
    }
}
