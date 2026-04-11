using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Integration tests for <see cref="CommunityService"/> against a real
/// MongoDB Testcontainer. Covers every invariant, rate limit, and
/// authorization rule from the plan's Sprint 2 S2.1 acceptance criteria.
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class CommunityServiceTests(MongoFixture fixture)
{
    /// <summary>
    /// <see cref="MongoFixture.CreateDbContext"/> generates a fresh
    /// per-call database, so all repositories a test uses must be
    /// constructed from the same <c>INinetyNineDbContext</c> instance.
    /// This helper returns the service plus the individual repos the
    /// test needs for direct state inspection, all wired to the same
    /// underlying database.
    /// </summary>
    private (ICommunityService Service,
             IPlayerRepository Players,
             IVenueRepository Venues,
             ICommunityMemberRepository Members)
        CreateService()
    {
        var ctx = fixture.CreateDbContext();
        var communities = new CommunityRepository(ctx, NullLogger<CommunityRepository>.Instance);
        var members = new CommunityMemberRepository(ctx, NullLogger<CommunityMemberRepository>.Instance);
        var invites = new CommunityInvitationRepository(ctx, NullLogger<CommunityInvitationRepository>.Instance);
        var joins = new CommunityJoinRequestRepository(ctx, NullLogger<CommunityJoinRequestRepository>.Instance);
        var players = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var venues = new VenueRepository(ctx, NullLogger<VenueRepository>.Instance);
        var svc = new CommunityService(
            communities, members, invites, joins, players, venues,
            NullLogger<CommunityService>.Instance);
        return (svc, players, venues, members);
    }

    // Back-compat shim so existing tests that only need the service + player repo
    // keep working after the CreateService refactor.
    private ICommunityService CreateService(out IPlayerRepository playerRepo)
    {
        var (svc, players, _, _) = CreateService();
        playerRepo = players;
        return svc;
    }

    private static async Task<Player> CreatePlayer(IPlayerRepository repo, string? displayName = null)
    {
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = displayName ?? "p_" + Guid.NewGuid().ToString("N")[..12],
            EmailAddress = Guid.NewGuid().ToString("N") + "@example.local",
            EmailVerified = true,
            SchemaVersion = 2,
        };
        await repo.CreateAsync(player);
        return player;
    }

    private static string UniqueName(string prefix)
        => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";

    private static string UniqueSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    // ── Create / update / delete / transfer ─────────────────────────

    [Fact]
    public async Task Create_Succeeds_AndAddsOwnerMembership()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);

        var result = await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Bumpers Regulars"), UniqueSlug("bumpers"),
            "Friends of Bumpers", CommunityVisibility.Private);

        result.Success.Should().BeTrue();
        result.Value!.OwnerPlayerId.Should().Be(owner.PlayerId);
        result.Value.Visibility.Should().Be(CommunityVisibility.Private);

        var mine = await svc.ListCommunitiesForPlayerAsync(owner.PlayerId);
        mine.Should().ContainSingle(c => c.CommunityId == result.Value.CommunityId);
    }

    [Fact]
    public async Task Create_RejectsDuplicateNameCaseInsensitive()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);
        var name = UniqueName("Clash");

        var first = await svc.CreatePlayerOwnedAsync(
            a.PlayerId, name, UniqueSlug("first"), null, CommunityVisibility.Public);
        first.Success.Should().BeTrue();

        var second = await svc.CreatePlayerOwnedAsync(
            b.PlayerId, name.ToUpperInvariant(), UniqueSlug("second"), null, CommunityVisibility.Public);
        second.Success.Should().BeFalse();
        second.ErrorCode.Should().Be("CommunityNameTaken");
    }

    [Fact]
    public async Task Create_EnforcesTenCommunitiesPerOwnerCap()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);

        for (int i = 0; i < 10; i++)
        {
            var r = await svc.CreatePlayerOwnedAsync(
                owner.PlayerId, UniqueName($"Cap{i}"), UniqueSlug($"cap{i}"),
                null, CommunityVisibility.Public);
            r.Success.Should().BeTrue();
        }

        var eleventh = await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Overflow"), UniqueSlug("overflow"),
            null, CommunityVisibility.Public);

        eleventh.Success.Should().BeFalse();
        eleventh.ErrorCode.Should().Be("CommunityCapExceeded");
    }

    [Fact]
    public async Task Update_OwnerOnly()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var bystander = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Updatable"), UniqueSlug("upd"),
            null, CommunityVisibility.Public)).Value!;

        var byBystander = await svc.UpdateAsync(
            c.CommunityId, bystander.PlayerId, new CommunityUpdate(Description: "hacked"));
        byBystander.Success.Should().BeFalse();
        byBystander.ErrorCode.Should().Be("NotAuthorized");

        var byOwner = await svc.UpdateAsync(
            c.CommunityId, owner.PlayerId, new CommunityUpdate(Description: "new desc"));
        byOwner.Success.Should().BeTrue();
        byOwner.Value!.Description.Should().Be("new desc");
    }

    [Fact]
    public async Task Delete_CascadesMembershipsAndClearsVenueAffiliation()
    {
        // All four artifacts MUST share one fixture database — see the
        // comment on CreateService() for why.
        var (svc, players, venues, members) = CreateService();

        var owner = await CreatePlayer(players);
        var friend = await CreatePlayer(players);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("ToDelete"), UniqueSlug("todel"),
            null, CommunityVisibility.Public)).Value!;

        // Friend joins.
        (await svc.JoinPublicAsync(c.CommunityId, friend.PlayerId))
            .Success.Should().BeTrue();

        // A venue affiliated with this community.
        var venue = new Venue
        {
            VenueId = Guid.NewGuid(),
            Name = UniqueName("AffilVenue"),
            CommunityId = c.CommunityId,
        };
        await venues.CreateAsync(venue);

        // Sanity-check the setup wrote what we think it wrote.
        (await venues.GetByIdAsync(venue.VenueId))!.CommunityId
            .Should().Be(c.CommunityId);

        var delete = await svc.DeleteAsync(c.CommunityId, owner.PlayerId);
        delete.Success.Should().BeTrue();

        (await members.CountMembersAsync(c.CommunityId)).Should().Be(0);
        (await venues.GetByIdAsync(venue.VenueId))!.CommunityId.Should().BeNull();
    }

    [Fact]
    public async Task TransferOwnership_RequiresTargetMember()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var stranger = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Transferable"), UniqueSlug("xfer"),
            null, CommunityVisibility.Public)).Value!;

        var notMember = await svc.TransferOwnershipAsync(
            c.CommunityId, stranger.PlayerId, owner.PlayerId);
        notMember.Success.Should().BeFalse();
        notMember.ErrorCode.Should().Be("NotAMember");
    }

    [Fact]
    public async Task TransferOwnership_FlipsRoles()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var successor = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Transferred"), UniqueSlug("xfer2"),
            null, CommunityVisibility.Public)).Value!;

        (await svc.JoinPublicAsync(c.CommunityId, successor.PlayerId)).Success.Should().BeTrue();

        var transfer = await svc.TransferOwnershipAsync(
            c.CommunityId, successor.PlayerId, owner.PlayerId);
        transfer.Success.Should().BeTrue();

        var refreshed = (await svc.GetForViewerAsync(c.CommunityId, successor.PlayerId))!;
        refreshed.OwnerPlayerId.Should().Be(successor.PlayerId);
    }

    // ── Invitations ─────────────────────────────────────────────────

    [Fact]
    public async Task Invite_OwnerOnly()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var bystander = await CreatePlayer(repo);
        var target = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("InvOnly"), UniqueSlug("invonly"),
            null, CommunityVisibility.Private)).Value!;

        var byBystander = await svc.InviteAsync(c.CommunityId, target.PlayerId, bystander.PlayerId);
        byBystander.Success.Should().BeFalse();
        byBystander.ErrorCode.Should().Be("NotAuthorized");

        var byOwner = await svc.InviteAsync(c.CommunityId, target.PlayerId, owner.PlayerId);
        byOwner.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Invite_RejectsDuplicatePending()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var target = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("NoDupe"), UniqueSlug("nodupe"),
            null, CommunityVisibility.Private)).Value!;

        (await svc.InviteAsync(c.CommunityId, target.PlayerId, owner.PlayerId))
            .Success.Should().BeTrue();

        var dupe = await svc.InviteAsync(c.CommunityId, target.PlayerId, owner.PlayerId);
        dupe.Success.Should().BeFalse();
        dupe.ErrorCode.Should().Be("InviteAlreadyPending");
    }

    [Fact]
    public async Task Invite_Accept_CreatesMembership()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var target = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Accept"), UniqueSlug("accept"),
            null, CommunityVisibility.Private)).Value!;

        var invite = (await svc.InviteAsync(c.CommunityId, target.PlayerId, owner.PlayerId)).Value!;

        (await svc.RespondToInvitationAsync(invite.InvitationId, target.PlayerId, accept: true))
            .Success.Should().BeTrue();

        (await svc.IsMemberAsync(c.CommunityId, target.PlayerId)).Should().BeTrue();
    }

    [Fact]
    public async Task Invite_OnlyInviteeMayRespond()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var target = await CreatePlayer(repo);
        var rando = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Respond"), UniqueSlug("respond"),
            null, CommunityVisibility.Private)).Value!;

        var invite = (await svc.InviteAsync(c.CommunityId, target.PlayerId, owner.PlayerId)).Value!;

        var byRando = await svc.RespondToInvitationAsync(
            invite.InvitationId, rando.PlayerId, accept: true);
        byRando.Success.Should().BeFalse();
        byRando.ErrorCode.Should().Be("NotAuthorized");
    }

    // ── Join requests ───────────────────────────────────────────────

    [Fact]
    public async Task JoinPublic_RejectedForPrivateCommunity()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var outsider = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Locked"), UniqueSlug("locked"),
            null, CommunityVisibility.Private)).Value!;

        var result = await svc.JoinPublicAsync(c.CommunityId, outsider.PlayerId);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("PrivateCommunityRequiresInvite");
    }

    [Fact]
    public async Task JoinPublic_OneClickPublic_AddsMembership()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var joiner = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Open"), UniqueSlug("open"),
            null, CommunityVisibility.Public)).Value!;

        (await svc.JoinPublicAsync(c.CommunityId, joiner.PlayerId)).Success.Should().BeTrue();
        (await svc.IsMemberAsync(c.CommunityId, joiner.PlayerId)).Should().BeTrue();
    }

    [Fact]
    public async Task RequestToJoin_ApproveFlow()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var joiner = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("PrivateJoin"), UniqueSlug("privjoin"),
            null, CommunityVisibility.Private)).Value!;

        var request = (await svc.RequestToJoinAsync(c.CommunityId, joiner.PlayerId)).Value!;

        var approve = await svc.ApproveJoinRequestAsync(request.RequestId, owner.PlayerId);
        approve.Success.Should().BeTrue();

        (await svc.IsMemberAsync(c.CommunityId, joiner.PlayerId)).Should().BeTrue();
    }

    [Fact]
    public async Task RequestToJoin_DuplicatePendingRejected()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var joiner = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Dupe"), UniqueSlug("dupereq"),
            null, CommunityVisibility.Private)).Value!;

        (await svc.RequestToJoinAsync(c.CommunityId, joiner.PlayerId)).Success.Should().BeTrue();
        var dupe = await svc.RequestToJoinAsync(c.CommunityId, joiner.PlayerId);
        dupe.Success.Should().BeFalse();
        dupe.ErrorCode.Should().Be("JoinRequestAlreadyPending");
    }

    // ── Leave / remove ──────────────────────────────────────────────

    [Fact]
    public async Task Leave_SoleOwnerCannotLeave()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("Sole"), UniqueSlug("sole"),
            null, CommunityVisibility.Public)).Value!;

        var result = await svc.LeaveAsync(c.CommunityId, owner.PlayerId);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("OwnerCannotLeave");
    }

    [Fact]
    public async Task RemoveMember_OwnerCannotBeRemoved()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("KeepOwner"), UniqueSlug("keepowner"),
            null, CommunityVisibility.Public)).Value!;

        var result = await svc.RemoveMemberAsync(c.CommunityId, owner.PlayerId, owner.PlayerId);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CannotRemoveSelf");
    }

    // ── Visibility / privacy ────────────────────────────────────────

    [Fact]
    public async Task GetForViewer_PrivateHiddenFromNonMembers()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var outsider = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("HiddenFromStrangers"), UniqueSlug("hidden"),
            null, CommunityVisibility.Private)).Value!;

        (await svc.GetForViewerAsync(c.CommunityId, outsider.PlayerId)).Should().BeNull();
        (await svc.GetForViewerAsync(c.CommunityId, null)).Should().BeNull();
        (await svc.GetForViewerAsync(c.CommunityId, owner.PlayerId)).Should().NotBeNull();
    }

    [Fact]
    public async Task ListMembers_PrivateHiddenFromNonMembers()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var member = await CreatePlayer(repo);
        var outsider = await CreatePlayer(repo);

        var c = (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, UniqueName("MembersPriv"), UniqueSlug("memberspriv"),
            null, CommunityVisibility.Private)).Value!;

        var inv = (await svc.InviteAsync(c.CommunityId, member.PlayerId, owner.PlayerId)).Value!;
        await svc.RespondToInvitationAsync(inv.InvitationId, member.PlayerId, accept: true);

        // Member can see the full list.
        (await svc.ListMembersAsync(c.CommunityId, owner.PlayerId))
            .Should().HaveCount(2);

        // Outsider sees nothing.
        (await svc.ListMembersAsync(c.CommunityId, outsider.PlayerId))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task BrowsePublic_ExcludesPrivate()
    {
        var svc = CreateService(out var repo);
        var owner = await CreatePlayer(repo);
        var prefix = "Br" + Guid.NewGuid().ToString("N")[..6];

        (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, $"{prefix} Pub 1", UniqueSlug("pub1"),
            null, CommunityVisibility.Public)).Success.Should().BeTrue();
        (await svc.CreatePlayerOwnedAsync(
            owner.PlayerId, $"{prefix} Secret", UniqueSlug("secret"),
            null, CommunityVisibility.Private)).Success.Should().BeTrue();

        var results = await svc.BrowsePublicAsync(prefix);
        results.Should().HaveCount(1);
        results[0].Visibility.Should().Be(CommunityVisibility.Public);
    }
}
