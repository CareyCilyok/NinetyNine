using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Repository.Storage;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Integration tests for <see cref="IPlayerService.GetProfileForViewerAsync"/>
/// exercising the full viewer-relationship × field-audience matrix locked
/// in Sprint 3 S3.5. The 16+ combinations required by the plan DoD are
/// parameterized; additional tests cover the anonymous tier and the
/// relationship-resolution order.
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class PlayerServiceProfileForViewerTests(MongoFixture fixture)
{
    /// <summary>
    /// <see cref="MongoFixture.CreateDbContext"/> returns a fresh
    /// per-call database, so every repository must share one context.
    /// This helper returns the player service plus the raw repos the
    /// test needs for direct state setup (friendships, communities).
    /// </summary>
    private (IPlayerService Service,
             IPlayerRepository Players,
             IFriendshipRepository Friendships,
             ICommunityMemberRepository Members,
             ICommunityRepository Communities)
        CreateService()
    {
        var ctx = fixture.CreateDbContext();
        var players = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var avatarStore = new GridFsAvatarStore(ctx, NullLogger<GridFsAvatarStore>.Instance);
        var avatarSvc = new AvatarService(avatarStore, NullLogger<AvatarService>.Instance);
        var friends = new FriendshipRepository(ctx, NullLogger<FriendshipRepository>.Instance);
        var members = new CommunityMemberRepository(ctx, NullLogger<CommunityMemberRepository>.Instance);
        var communities = new CommunityRepository(ctx, NullLogger<CommunityRepository>.Instance);

        var svc = new PlayerService(
            players, avatarStore, avatarSvc,
            friends, members,
            NullLogger<PlayerService>.Instance);

        return (svc, players, friends, members, communities);
    }

    private static async Task<Player> CreatePlayer(
        IPlayerRepository repo,
        Audience emailAudience = Audience.Private,
        Audience phoneAudience = Audience.Private,
        Audience realNameAudience = Audience.Private,
        Audience avatarAudience = Audience.Public)
    {
        var p = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "p_" + Guid.NewGuid().ToString("N")[..12],
            EmailAddress = Guid.NewGuid().ToString("N") + "@example.local",
            EmailVerified = true,
            PhoneNumber = "+1-555-0100",
            FirstName = "First",
            MiddleName = "Mid",
            LastName = "Last",
            Avatar = new AvatarRef
            {
                StorageKey = Guid.NewGuid().ToString("N"),
                ContentType = "image/png",
                WidthPx = 128,
                HeightPx = 128,
                SizeBytes = 1024,
            },
            Visibility = new ProfileVisibility
            {
                EmailAudience = emailAudience,
                PhoneAudience = phoneAudience,
                RealNameAudience = realNameAudience,
                AvatarAudience = avatarAudience,
            },
            SchemaVersion = 2,
        };
        await repo.CreateAsync(p);
        return p;
    }

    private static async Task MakeFriendsAsync(
        IFriendshipRepository repo, Guid a, Guid b)
    {
        await repo.CreateAsync(Friendship.Create(a, b, initiatedBy: a, via: "test"));
    }

    /// <summary>
    /// Creates a community and adds both players as approved members
    /// so they satisfy the shared-community check. The community's
    /// visibility is irrelevant to the audience matrix — both public
    /// and private communities count.
    /// </summary>
    private static async Task MakeCoMembersAsync(
        ICommunityRepository communityRepo,
        ICommunityMemberRepository memberRepo,
        Guid a, Guid b)
    {
        var community = new Community
        {
            CommunityId = Guid.NewGuid(),
            Name = "Shared " + Guid.NewGuid().ToString("N")[..8],
            Slug = "shared-" + Guid.NewGuid().ToString("N")[..8],
            OwnerPlayerId = a,
            CreatedByPlayerId = a,
            Visibility = CommunityVisibility.Public,
            SchemaVersion = 2,
        };
        await communityRepo.CreateAsync(community);

        await memberRepo.AddAsync(new CommunityMembership
        {
            MembershipId = Guid.NewGuid(),
            CommunityId = community.CommunityId,
            PlayerId = a,
            Role = CommunityRole.Owner,
            JoinedAt = DateTime.UtcNow,
        });
        await memberRepo.AddAsync(new CommunityMembership
        {
            MembershipId = Guid.NewGuid(),
            CommunityId = community.CommunityId,
            PlayerId = b,
            Role = CommunityRole.Member,
            JoinedAt = DateTime.UtcNow,
        });
    }

    // ── Null / not-found ────────────────────────────────────────────────

    [Fact]
    public async Task Returns_Null_WhenTargetNotFound()
    {
        var (svc, _, _, _, _) = CreateService();

        var result = await svc.GetProfileForViewerAsync(
            targetId: Guid.NewGuid(), viewerId: Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── Self relationship sees everything ───────────────────────────────

    [Fact]
    public async Task Self_SeesEveryField_RegardlessOfAudience()
    {
        var (svc, players, _, _, _) = CreateService();
        var target = await CreatePlayer(players,
            emailAudience: Audience.Private,
            phoneAudience: Audience.Private,
            realNameAudience: Audience.Private,
            avatarAudience: Audience.Private);

        var profile = await svc.GetProfileForViewerAsync(
            targetId: target.PlayerId, viewerId: target.PlayerId);

        profile.Should().NotBeNull();
        profile!.Relationship.Should().Be(ViewerRelationship.Self);
        profile.IsOwnProfile.Should().BeTrue();
        profile.EmailAddress.Should().Be(target.EmailAddress);
        profile.PhoneNumber.Should().Be(target.PhoneNumber);
        profile.FirstName.Should().Be(target.FirstName);
        profile.MiddleName.Should().Be(target.MiddleName);
        profile.LastName.Should().Be(target.LastName);
        profile.Avatar.Should().NotBeNull();
    }

    // ── Full 16-combo audience matrix ───────────────────────────────────
    // Each row represents one relationship (minus Self — see dedicated
    // test above) paired with one field audience. The expected column
    // is computed directly from the locked semantic
    // `(int)relationship <= (int)audience`, which is the exact rule
    // the implementation must follow.

    public static readonly TheoryData<ViewerRelationship, Audience, bool> Matrix = new()
    {
        // Friend viewer (floor = Friends=1)
        { ViewerRelationship.Friend, Audience.Private,      false },
        { ViewerRelationship.Friend, Audience.Friends,      true  },
        { ViewerRelationship.Friend, Audience.Communities,  true  },
        { ViewerRelationship.Friend, Audience.Public,       true  },

        // CommunityMember viewer (floor = Communities=2)
        { ViewerRelationship.CommunityMember, Audience.Private,     false },
        { ViewerRelationship.CommunityMember, Audience.Friends,     false },
        { ViewerRelationship.CommunityMember, Audience.Communities, true  },
        { ViewerRelationship.CommunityMember, Audience.Public,      true  },

        // Public viewer (any authenticated non-friend non-co-member)
        { ViewerRelationship.Public, Audience.Private,     false },
        { ViewerRelationship.Public, Audience.Friends,     false },
        { ViewerRelationship.Public, Audience.Communities, false },
        { ViewerRelationship.Public, Audience.Public,      true  },

        // Anonymous viewer (unauthenticated)
        { ViewerRelationship.Anonymous, Audience.Private,     false },
        { ViewerRelationship.Anonymous, Audience.Friends,     false },
        { ViewerRelationship.Anonymous, Audience.Communities, false },
        { ViewerRelationship.Anonymous, Audience.Public,      true  },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Matrix_GatesEveryFieldCorrectly(
        ViewerRelationship relationship, Audience fieldAudience, bool expectedVisible)
    {
        // Set every field to the same audience so one gate call exercises all four fields.
        var (svc, players, friends, members, communities) = CreateService();
        var target = await CreatePlayer(players,
            emailAudience: fieldAudience,
            phoneAudience: fieldAudience,
            realNameAudience: fieldAudience,
            avatarAudience: fieldAudience);

        Guid? viewerId;
        if (relationship == ViewerRelationship.Anonymous)
        {
            viewerId = null;
        }
        else
        {
            var viewer = await CreatePlayer(players);
            viewerId = viewer.PlayerId;

            if (relationship == ViewerRelationship.Friend)
                await MakeFriendsAsync(friends, viewer.PlayerId, target.PlayerId);
            else if (relationship == ViewerRelationship.CommunityMember)
                await MakeCoMembersAsync(communities, members, viewer.PlayerId, target.PlayerId);
            // ViewerRelationship.Public → no extra setup; plain authenticated viewer.
        }

        var profile = await svc.GetProfileForViewerAsync(target.PlayerId, viewerId);

        profile.Should().NotBeNull();
        profile!.Relationship.Should().Be(relationship);

        if (expectedVisible)
        {
            profile.EmailAddress.Should().Be(target.EmailAddress,
                "email audience tier allows this viewer");
            profile.PhoneNumber.Should().Be(target.PhoneNumber,
                "phone audience tier allows this viewer");
            profile.FirstName.Should().Be(target.FirstName,
                "real-name audience tier allows this viewer");
            profile.Avatar.Should().NotBeNull("avatar audience tier allows this viewer");
        }
        else
        {
            profile.EmailAddress.Should().BeNull("email audience tier excludes this viewer");
            profile.PhoneNumber.Should().BeNull("phone audience tier excludes this viewer");
            profile.FirstName.Should().BeNull("real-name audience tier excludes this viewer");
            profile.MiddleName.Should().BeNull();
            profile.LastName.Should().BeNull();
            profile.Avatar.Should().BeNull("avatar audience tier excludes this viewer");
        }
    }

    // ── Display name + creation timestamp are never gated ───────────────

    [Fact]
    public async Task DisplayName_AndCreatedAt_AlwaysVisibleRegardlessOfRelationship()
    {
        var (svc, players, _, _, _) = CreateService();
        var target = await CreatePlayer(players,
            emailAudience: Audience.Private,
            phoneAudience: Audience.Private,
            realNameAudience: Audience.Private,
            avatarAudience: Audience.Private);

        // Anonymous is the most restrictive; if display name leaks through
        // there it leaks through everywhere.
        var anon = await svc.GetProfileForViewerAsync(target.PlayerId, viewerId: null);

        anon.Should().NotBeNull();
        anon!.DisplayName.Should().Be(target.DisplayName);
        // BSON stores DateTime at millisecond precision, so round-trip
        // through Mongo loses the sub-ms ticks from DateTime.UtcNow.
        anon.CreatedAt.Should().BeCloseTo(target.CreatedAt, TimeSpan.FromMilliseconds(1));
        anon.PlayerId.Should().Be(target.PlayerId);
    }

    // ── Field independence — per-field audiences don't cross-contaminate ─

    [Fact]
    public async Task PerField_AudiencesAreIndependent()
    {
        var (svc, players, friends, _, _) = CreateService();
        var target = await CreatePlayer(players,
            emailAudience: Audience.Friends,   // friend sees
            phoneAudience: Audience.Private,    // friend does NOT see
            realNameAudience: Audience.Public,  // everyone sees
            avatarAudience: Audience.Public);
        var viewer = await CreatePlayer(players);
        await MakeFriendsAsync(friends, viewer.PlayerId, target.PlayerId);

        var profile = await svc.GetProfileForViewerAsync(target.PlayerId, viewer.PlayerId);

        profile.Should().NotBeNull();
        profile!.Relationship.Should().Be(ViewerRelationship.Friend);
        profile.EmailAddress.Should().Be(target.EmailAddress);
        profile.PhoneNumber.Should().BeNull();
        profile.FirstName.Should().Be(target.FirstName);
        profile.Avatar.Should().NotBeNull();
    }

    // ── Relationship resolution order ───────────────────────────────────

    [Fact]
    public async Task ResolutionOrder_FriendBeatsCommunityMembership()
    {
        // When both friendship and shared community exist, the viewer
        // should resolve as Friend (the stricter / higher-privilege tier).
        var (svc, players, friends, members, communities) = CreateService();
        var target = await CreatePlayer(players,
            emailAudience: Audience.Friends,     // friend can see
            phoneAudience: Audience.Communities, // both tiers can see
            realNameAudience: Audience.Private,  // neither can see
            avatarAudience: Audience.Public);
        var viewer = await CreatePlayer(players);

        await MakeFriendsAsync(friends, viewer.PlayerId, target.PlayerId);
        await MakeCoMembersAsync(communities, members, viewer.PlayerId, target.PlayerId);

        var profile = await svc.GetProfileForViewerAsync(target.PlayerId, viewer.PlayerId);

        profile.Should().NotBeNull();
        profile!.Relationship.Should().Be(ViewerRelationship.Friend);
        profile.EmailAddress.Should().Be(target.EmailAddress);
    }

    [Fact]
    public async Task ResolutionOrder_CommunityMemberBeatsPublic()
    {
        // When a viewer shares a community with the target but has no
        // friendship, the resolved tier must be CommunityMember, not Public.
        var (svc, players, _, members, communities) = CreateService();
        var target = await CreatePlayer(players,
            phoneAudience: Audience.Communities,
            avatarAudience: Audience.Public);
        var viewer = await CreatePlayer(players);
        await MakeCoMembersAsync(communities, members, viewer.PlayerId, target.PlayerId);

        var profile = await svc.GetProfileForViewerAsync(target.PlayerId, viewer.PlayerId);

        profile.Should().NotBeNull();
        profile!.Relationship.Should().Be(ViewerRelationship.CommunityMember);
        profile.PhoneNumber.Should().Be(target.PhoneNumber);
    }

    [Fact]
    public async Task ResolutionOrder_UnaffiliatedAuthenticatedIsPublic()
    {
        var (svc, players, _, _, _) = CreateService();
        var target = await CreatePlayer(players, avatarAudience: Audience.Public);
        var viewer = await CreatePlayer(players);

        var profile = await svc.GetProfileForViewerAsync(target.PlayerId, viewer.PlayerId);

        profile.Should().NotBeNull();
        profile!.Relationship.Should().Be(ViewerRelationship.Public);
        profile.Avatar.Should().NotBeNull();
    }

    // ── Shared-community edge cases ─────────────────────────────────────

    [Fact]
    public async Task SharedCommunityCheck_BothMustBeMembers()
    {
        // Target belongs to a community; viewer does not. Must NOT
        // resolve as CommunityMember — the shared-membership check
        // requires BOTH parties to be current members.
        var (svc, players, _, members, communities) = CreateService();
        var target = await CreatePlayer(players, phoneAudience: Audience.Communities);
        var viewer = await CreatePlayer(players);

        // Only target is in the community.
        var c = new Community
        {
            CommunityId = Guid.NewGuid(),
            Name = "Solo " + Guid.NewGuid().ToString("N")[..8],
            Slug = "solo-" + Guid.NewGuid().ToString("N")[..8],
            OwnerPlayerId = target.PlayerId,
            CreatedByPlayerId = target.PlayerId,
            Visibility = CommunityVisibility.Public,
            SchemaVersion = 2,
        };
        await communities.CreateAsync(c);
        await members.AddAsync(new CommunityMembership
        {
            MembershipId = Guid.NewGuid(),
            CommunityId = c.CommunityId,
            PlayerId = target.PlayerId,
            Role = CommunityRole.Owner,
            JoinedAt = DateTime.UtcNow,
        });

        var profile = await svc.GetProfileForViewerAsync(target.PlayerId, viewer.PlayerId);

        profile.Should().NotBeNull();
        profile!.Relationship.Should().Be(ViewerRelationship.Public);
        profile.PhoneNumber.Should().BeNull("viewer is not in the community");
    }
}
