using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NinetyNine.Model;
using NinetyNine.Repository;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Repository.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class PlayerRepositoryTests(MongoFixture fixture)
{
    private IPlayerRepository CreateRepo()
    {
        var ctx = fixture.CreateDbContext();
        return new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
    }

    private static Player MakePlayer(string displayName = "Alice")
    {
        return new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = displayName,
            EmailAddress = $"{displayName.ToLowerInvariant()}@example.local"
        };
    }

    [Fact]
    public async Task CreateAndGetById_RoundTrip()
    {
        var repo = CreateRepo();
        var player = MakePlayer("CreateGetById");
        await repo.CreateAsync(player);

        var retrieved = await repo.GetByIdAsync(player.PlayerId);
        retrieved.Should().NotBeNull();
        retrieved!.PlayerId.Should().Be(player.PlayerId);
        retrieved.DisplayName.Should().Be("CreateGetById");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = CreateRepo();
        var result = await repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByDisplayNameAsync_ReturnsPlayer()
    {
        var repo = CreateRepo();
        var player = MakePlayer("ByDisplayName");
        await repo.CreateAsync(player);

        var found = await repo.GetByDisplayNameAsync("ByDisplayName");
        found.Should().NotBeNull();
        found!.PlayerId.Should().Be(player.PlayerId);
    }

    [Fact]
    public async Task GetByDisplayNameAsync_ReturnsNull_WhenNotFound()
    {
        var repo = CreateRepo();
        var result = await repo.GetByDisplayNameAsync("NoSuchName_xyz");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DisplayNameExistsAsync_ReturnsTrue_WhenExists()
    {
        var repo = CreateRepo();
        var player = MakePlayer("ExistsTest");
        await repo.CreateAsync(player);

        var exists = await repo.DisplayNameExistsAsync("ExistsTest");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DisplayNameExistsAsync_ReturnsFalse_WhenNotExists()
    {
        var repo = CreateRepo();
        var exists = await repo.DisplayNameExistsAsync("DefinitelyNotThere_xyz");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var repo = CreateRepo();
        var player = MakePlayer("UpdateTest");
        await repo.CreateAsync(player);

        player.EmailAddress = "updated@example.com";
        player.FirstName = "Updated";
        await repo.UpdateAsync(player);

        var retrieved = await repo.GetByIdAsync(player.PlayerId);
        retrieved!.EmailAddress.Should().Be("updated@example.com");
        retrieved.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenPlayerNotFound()
    {
        var repo = CreateRepo();
        var ghost = MakePlayer("GhostPlayer");
        // Not inserted — updating should throw
        var act = async () => await repo.UpdateAsync(ghost);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesPlayer()
    {
        var repo = CreateRepo();
        var player = MakePlayer("DeleteTest");
        await repo.CreateAsync(player);

        await repo.DeleteAsync(player.PlayerId);
        var retrieved = await repo.GetByIdAsync(player.PlayerId);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_ReturnsMatchingPlayers()
    {
        var repo = CreateRepo();
        var prefix = $"Search_{Guid.NewGuid():N}";
        var p1 = MakePlayer($"{prefix}_Alpha");
        var p2 = MakePlayer($"{prefix}_Beta");
        var p3 = MakePlayer("Unrelated_xyz");
        await repo.CreateAsync(p1);
        await repo.CreateAsync(p2);
        await repo.CreateAsync(p3);

        var results = await repo.SearchAsync(prefix, limit: 10);
        results.Should().HaveCount(2);
        results.Select(r => r.DisplayName).Should().Contain($"{prefix}_Alpha");
        results.Select(r => r.DisplayName).Should().Contain($"{prefix}_Beta");
    }

    [Fact]
    public async Task SearchAsync_RespectsLimit()
    {
        var repo = CreateRepo();
        var prefix = $"Limit_{Guid.NewGuid():N}";
        for (int i = 0; i < 5; i++)
            await repo.CreateAsync(MakePlayer($"{prefix}_{i}"));

        var results = await repo.SearchAsync(prefix, limit: 3);
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task UniqueIndex_OnDisplayName_PreventsInsertDuplicate()
    {
        var repo = CreateRepo();
        var player1 = MakePlayer("DuplicateName");
        var player2 = MakePlayer("DuplicateName");
        await repo.CreateAsync(player1);

        var act = async () => await repo.CreateAsync(player2);
        await act.Should().ThrowAsync<Exception>("duplicate display names violate unique index");
    }

}
