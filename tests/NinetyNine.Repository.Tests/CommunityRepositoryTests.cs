using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Repository.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class CommunityRepositoryTests(MongoFixture fixture)
{
    private ICommunityRepository CreateRepo()
    {
        var ctx = fixture.CreateDbContext();
        return new CommunityRepository(ctx, NullLogger<CommunityRepository>.Instance);
    }

    private static Community MakePlayerOwned(string name, CommunityVisibility visibility = CommunityVisibility.Public)
        => new()
        {
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-') + "-" + Guid.NewGuid().ToString("N")[..8],
            Description = "Test community",
            Visibility = visibility,
            OwnerPlayerId = Guid.NewGuid(),
            CreatedByPlayerId = Guid.NewGuid(),
        };

    // Note: post-2026-04-11 principle update, venue-owned communities
    // are impossible. The venue-owned helper + persistence test have been
    // removed. Venue ↔ community is affiliation-only via Venue.CommunityId
    // (Sprint 3 S3.2).

    [Fact]
    public async Task CreateAndGet_RoundTrip()
    {
        var repo = CreateRepo();
        var c = MakePlayerOwned($"Bumpers Regulars {Guid.NewGuid():N}");

        await repo.CreateAsync(c);
        var retrieved = await repo.GetByIdAsync(c.CommunityId);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(c.Name);
        retrieved.OwnerPlayerId.Should().Be(c.OwnerPlayerId);
    }

    [Fact]
    public async Task DuplicateName_IsRejectedCaseInsensitive()
    {
        var repo = CreateRepo();
        var name = $"League {Guid.NewGuid():N}";

        var first = MakePlayerOwned(name);
        await repo.CreateAsync(first);

        // Same logical name in mixed case, different slug — must collide.
        var second = new Community
        {
            Name = name.ToUpperInvariant(),
            Slug = "different-slug-" + Guid.NewGuid().ToString("N")[..8],
            Visibility = CommunityVisibility.Public,
            OwnerPlayerId = Guid.NewGuid(),
            CreatedByPlayerId = Guid.NewGuid(),
        };

        var act = async () => await repo.CreateAsync(second);
        await act.Should().ThrowAsync<MongoDB.Driver.MongoWriteException>(
            "the case-insensitive unique index on name must reject the dupe");
    }

    [Fact]
    public async Task GetByNameAsync_IsCaseInsensitive()
    {
        var repo = CreateRepo();
        var name = $"Mixed Case {Guid.NewGuid():N}";
        await repo.CreateAsync(MakePlayerOwned(name));

        var found = await repo.GetByNameAsync(name.ToUpperInvariant());
        found.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchPublicByNameAsync_ExcludesPrivateCommunities()
    {
        var repo = CreateRepo();
        var prefix = $"Search{Guid.NewGuid():N}";

        await repo.CreateAsync(MakePlayerOwned($"{prefix} Public A"));
        await repo.CreateAsync(MakePlayerOwned($"{prefix} Public B"));
        await repo.CreateAsync(MakePlayerOwned($"{prefix} Secret", CommunityVisibility.Private));

        var results = await repo.SearchPublicByNameAsync(prefix);
        results.Should().HaveCount(2);
        results.Should().OnlyContain(c => c.Visibility == CommunityVisibility.Public);
    }

}
