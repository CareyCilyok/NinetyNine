using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Repository.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class VenueRepositoryTests(MongoFixture fixture)
{
    private IVenueRepository CreateRepo()
    {
        var ctx = fixture.CreateDbContext();
        return new VenueRepository(ctx, NullLogger<VenueRepository>.Instance);
    }

    private static Venue MakeVenue(string name, bool isPrivate = false) =>
        new Venue
        {
            VenueId = Guid.NewGuid(),
            Name = name,
            Address = "123 Main St",
            PhoneNumber = "555-0100",
            Private = isPrivate
        };

    [Fact]
    public async Task CreateAndGetById_RoundTrip()
    {
        var repo = CreateRepo();
        var venue = MakeVenue("Pool Palace");
        await repo.CreateAsync(venue);

        var retrieved = await repo.GetByIdAsync(venue.VenueId);
        retrieved.Should().NotBeNull();
        retrieved!.VenueId.Should().Be(venue.VenueId);
        retrieved.Name.Should().Be("Pool Palace");
        retrieved.Address.Should().Be("123 Main St");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = CreateRepo();
        var result = await repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_IncludePrivate_ReturnsAll()
    {
        var repo = CreateRepo();
        await repo.CreateAsync(MakeVenue("Public1", isPrivate: false));
        await repo.CreateAsync(MakeVenue("Private1", isPrivate: true));

        var all = await repo.GetAllAsync(includePrivate: true);
        all.Should().HaveCountGreaterThanOrEqualTo(2);
        all.Select(v => v.Name).Should().Contain("Private1");
    }

    [Fact]
    public async Task GetAllAsync_ExcludePrivate_FiltersOutPrivateVenues()
    {
        var repo = CreateRepo();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await repo.CreateAsync(MakeVenue($"Public_{suffix}", isPrivate: false));
        await repo.CreateAsync(MakeVenue($"Private_{suffix}", isPrivate: true));

        var publicOnly = await repo.GetAllAsync(includePrivate: false);
        publicOnly.Select(v => v.Name).Should().NotContain($"Private_{suffix}");
        publicOnly.Select(v => v.Name).Should().Contain($"Public_{suffix}");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var repo = CreateRepo();
        var venue = MakeVenue("Original Name");
        await repo.CreateAsync(venue);

        venue.Name = "Updated Name";
        venue.PhoneNumber = "555-9999";
        await repo.UpdateAsync(venue);

        var retrieved = await repo.GetByIdAsync(venue.VenueId);
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.PhoneNumber.Should().Be("555-9999");
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenNotFound()
    {
        var repo = CreateRepo();
        var ghost = MakeVenue("Ghost Venue");
        var act = async () => await repo.UpdateAsync(ghost);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesVenue()
    {
        var repo = CreateRepo();
        var venue = MakeVenue("ToDelete");
        await repo.CreateAsync(venue);

        await repo.DeleteAsync(venue.VenueId);
        var result = await repo.GetByIdAsync(venue.VenueId);
        result.Should().BeNull();
    }
}
