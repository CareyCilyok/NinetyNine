using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Repository.Tests;

/// <summary>
/// Integration tests for <see cref="FriendshipRepository"/>. Every test
/// uses fresh Guids so the Testcontainers Mongo instance need not be
/// cleaned between tests.
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class FriendshipRepositoryTests(MongoFixture fixture)
{
    private IFriendshipRepository CreateRepo()
    {
        var ctx = fixture.CreateDbContext();
        return new FriendshipRepository(ctx, NullLogger<FriendshipRepository>.Instance);
    }

    [Fact]
    public async Task CreateAndGetByPair_RoundTrip()
    {
        var repo = CreateRepo();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var friendship = Friendship.Create(a, b, initiatedBy: a);

        await repo.CreateAsync(friendship);

        var retrieved = await repo.GetByPairAsync(a, b);
        retrieved.Should().NotBeNull();
        retrieved!.PlayerIdsKey.Should().Be(friendship.PlayerIdsKey);
    }

    [Fact]
    public async Task GetByPairAsync_IsOrderIndependent()
    {
        var repo = CreateRepo();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await repo.CreateAsync(Friendship.Create(a, b));

        var forward = await repo.GetByPairAsync(a, b);
        var reverse = await repo.GetByPairAsync(b, a);

        forward.Should().NotBeNull();
        reverse.Should().NotBeNull();
        reverse!.FriendshipId.Should().Be(forward!.FriendshipId);
    }

    [Fact]
    public async Task ListForPlayerAsync_ReturnsEveryFriendshipInvolvingThePlayer()
    {
        var repo = CreateRepo();
        var me = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var f2 = Guid.NewGuid();
        var f3 = Guid.NewGuid();

        await repo.CreateAsync(Friendship.Create(me, f1));
        await repo.CreateAsync(Friendship.Create(me, f2));
        await repo.CreateAsync(Friendship.Create(me, f3));

        var mine = await repo.ListForPlayerAsync(me);
        mine.Should().HaveCount(3);
        mine.Select(x => x.OtherParty(me)).Should().Contain(new[] { f1, f2, f3 });
    }

    [Fact]
    public async Task DuplicateFriendship_IsRejectedByUniqueIndex()
    {
        var repo = CreateRepo();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await repo.CreateAsync(Friendship.Create(a, b));

        var act = async () => await repo.CreateAsync(Friendship.Create(b, a));
        await act.Should().ThrowAsync<MongoDB.Driver.MongoWriteException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesFriendship()
    {
        var repo = CreateRepo();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await repo.CreateAsync(Friendship.Create(a, b));

        await repo.DeleteAsync(a, b);

        var after = await repo.GetByPairAsync(a, b);
        after.Should().BeNull();
    }

    [Fact]
    public async Task CountForPlayerAsync_ReturnsAccurateCount()
    {
        var repo = CreateRepo();
        var me = Guid.NewGuid();
        await repo.CreateAsync(Friendship.Create(me, Guid.NewGuid()));
        await repo.CreateAsync(Friendship.Create(me, Guid.NewGuid()));

        var count = await repo.CountForPlayerAsync(me);
        count.Should().Be(2);
    }
}
