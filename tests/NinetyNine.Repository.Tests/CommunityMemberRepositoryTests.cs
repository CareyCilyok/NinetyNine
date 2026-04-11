using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Repository.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class CommunityMemberRepositoryTests(MongoFixture fixture)
{
    private ICommunityMemberRepository CreateRepo()
    {
        var ctx = fixture.CreateDbContext();
        return new CommunityMemberRepository(ctx, NullLogger<CommunityMemberRepository>.Instance);
    }

    private static CommunityMembership MakeMembership(
        Guid communityId,
        Guid playerId,
        CommunityRole role = CommunityRole.Member)
        => new()
        {
            CommunityId = communityId,
            PlayerId = playerId,
            Role = role,
            JoinedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task AddAndGetMembership_RoundTrip()
    {
        var repo = CreateRepo();
        var communityId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        await repo.AddAsync(MakeMembership(communityId, playerId, CommunityRole.Owner));

        var m = await repo.GetMembershipAsync(communityId, playerId);
        m.Should().NotBeNull();
        m!.Role.Should().Be(CommunityRole.Owner);
    }

    [Fact]
    public async Task DuplicateMembership_IsRejectedByUniqueIndex()
    {
        var repo = CreateRepo();
        var communityId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        await repo.AddAsync(MakeMembership(communityId, playerId));

        var act = async () => await repo.AddAsync(MakeMembership(communityId, playerId));
        await act.Should().ThrowAsync<MongoDB.Driver.MongoWriteException>();
    }

    [Fact]
    public async Task ListMembersAsync_OrdersByJoinedAt()
    {
        var repo = CreateRepo();
        var communityId = Guid.NewGuid();

        var first = MakeMembership(communityId, Guid.NewGuid());
        first.JoinedAt = DateTime.UtcNow.AddMinutes(-10);
        await repo.AddAsync(first);

        var second = MakeMembership(communityId, Guid.NewGuid());
        second.JoinedAt = DateTime.UtcNow.AddMinutes(-5);
        await repo.AddAsync(second);

        var third = MakeMembership(communityId, Guid.NewGuid());
        third.JoinedAt = DateTime.UtcNow;
        await repo.AddAsync(third);

        var members = await repo.ListMembersAsync(communityId);
        members.Should().HaveCount(3);
        members[0].PlayerId.Should().Be(first.PlayerId);
        members[1].PlayerId.Should().Be(second.PlayerId);
        members[2].PlayerId.Should().Be(third.PlayerId);
    }

    [Fact]
    public async Task ListCommunitiesForPlayerAsync_ReturnsAllMemberships()
    {
        var repo = CreateRepo();
        var playerId = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        var c3 = Guid.NewGuid();

        await repo.AddAsync(MakeMembership(c1, playerId));
        await repo.AddAsync(MakeMembership(c2, playerId));
        await repo.AddAsync(MakeMembership(c3, playerId));

        var mine = await repo.ListCommunitiesForPlayerAsync(playerId);
        mine.Should().HaveCount(3);
        mine.Select(m => m.CommunityId).Should().Contain(new[] { c1, c2, c3 });
    }

    [Fact]
    public async Task RemoveAsync_DeletesMembership()
    {
        var repo = CreateRepo();
        var communityId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        await repo.AddAsync(MakeMembership(communityId, playerId));
        await repo.RemoveAsync(communityId, playerId);

        var m = await repo.GetMembershipAsync(communityId, playerId);
        m.Should().BeNull();
    }

    [Fact]
    public async Task CountMembersAsync_ReturnsExactCount()
    {
        var repo = CreateRepo();
        var communityId = Guid.NewGuid();

        await repo.AddAsync(MakeMembership(communityId, Guid.NewGuid()));
        await repo.AddAsync(MakeMembership(communityId, Guid.NewGuid()));
        await repo.AddAsync(MakeMembership(communityId, Guid.NewGuid()));

        var count = await repo.CountMembersAsync(communityId);
        count.Should().Be(3);
    }
}
