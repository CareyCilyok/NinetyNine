using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository;
using NinetyNine.Repository.Repositories;
using NinetyNine.Repository.Storage;
using NinetyNine.Services;
using NinetyNine.Services.Models;

namespace NinetyNine.Services.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class PlayerServiceTests(MongoFixture fixture)
{
    private IPlayerService CreateService(out INinetyNineDbContext ctx)
    {
        ctx = fixture.CreateDbContext();
        var playerRepo = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var avatarStore = new GridFsAvatarStore(ctx, NullLogger<GridFsAvatarStore>.Instance);
        var avatarSvc = new AvatarService(avatarStore, NullLogger<AvatarService>.Instance);
        var friends = new FriendshipRepository(ctx, NullLogger<FriendshipRepository>.Instance);
        var members = new CommunityMemberRepository(ctx, NullLogger<CommunityMemberRepository>.Instance);
        return new PlayerService(playerRepo, avatarStore, avatarSvc,
            friends, members, NullLogger<PlayerService>.Instance);
    }

    private IPlayerService CreateService() => CreateService(out _);

    [Fact]
    public async Task RegisterAsync_CreatesPlayer()
    {
        var svc = CreateService();
        var player = await svc.RegisterAsync("NewPlayer1", "Mock", "mock-sub-001");

        player.Should().NotBeNull();
        player.DisplayName.Should().Be("NewPlayer1");
    }

    [Fact]
    public async Task RegisterAsync_Throws_WhenDisplayNameTaken()
    {
        var svc = CreateService();
        await svc.RegisterAsync("TakenName", "Google", Guid.NewGuid().ToString());

        var act = async () => await svc.RegisterAsync("TakenName", "Google", Guid.NewGuid().ToString());
        await act.Should().ThrowAsync<InvalidOperationException>(
            "duplicate display names should be rejected");
    }

    [Theory]
    [InlineData("x")]         // too short (1 char)
    [InlineData("")]          // empty
    [InlineData("this_name_is_way_too_long_to_be_valid_here")] // > 32 chars
    [InlineData("bad name!")]  // contains invalid characters (space, !)
    [InlineData("has space")]  // space not allowed
    public async Task RegisterAsync_Throws_WhenDisplayNameInvalid(string invalidName)
    {
        var svc = CreateService();
        var act = async () => await svc.RegisterAsync(invalidName, "Google", Guid.NewGuid().ToString());
        await act.Should().ThrowAsync<Exception>(
            $"display name '{invalidName}' should be rejected");
    }

    // LoginAsync is stubbed in WP-01 (returns null); WP-05 will re-implement
    // email/password login and restore these tests.
    [Fact]
    public async Task LoginAsync_ReturnsNull_Stub()
    {
        var svc = CreateService();
        var result = await svc.LoginAsync("Mock", "anyone");
        result.Should().BeNull("LoginAsync is a stub until WP-05 wires email/password auth");
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesFieldsAndVisibility()
    {
        var svc = CreateService();
        var providerUserId = Guid.NewGuid().ToString();
        var player = await svc.RegisterAsync("UpdateMe", "Google", providerUserId);

        var update = new PlayerProfileUpdate(
            DisplayName: null,
            EmailAddress: "me@example.com",
            PhoneNumber: "555-4321",
            FirstName: "Updated",
            MiddleName: null,
            LastName: "Last",
            Visibility: new ProfileVisibility { RealName = true, EmailAddress = true });

        var updated = await svc.UpdateProfileAsync(player.PlayerId, update);
        updated.EmailAddress.Should().Be("me@example.com");
        updated.PhoneNumber.Should().Be("555-4321");
        updated.FirstName.Should().Be("Updated");
        updated.LastName.Should().Be("Last");
        updated.Visibility.RealName.Should().BeTrue();
        updated.Visibility.EmailAddress.Should().BeTrue();
    }

    [Fact]
    public async Task IsDisplayNameAvailableAsync_ReturnsTrueWhenFree()
    {
        var svc = CreateService();
        var available = await svc.IsDisplayNameAvailableAsync("CompletelyFreeNameXYZ");
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsDisplayNameAvailableAsync_ReturnsFalseWhenTaken()
    {
        var svc = CreateService();
        await svc.RegisterAsync("TakenCheck", "Google", Guid.NewGuid().ToString());

        var available = await svc.IsDisplayNameAvailableAsync("TakenCheck");
        available.Should().BeFalse();
    }

    [Fact]
    public async Task IsDisplayNameAvailableAsync_ReturnsFalseWhenInvalidFormat()
    {
        var svc = CreateService();
        // Invalid format — service returns false without querying DB
        var available = await svc.IsDisplayNameAvailableAsync("invalid name!");
        available.Should().BeFalse();
    }

    [Fact]
    public async Task SetAvatarAsync_UploadsAndSetsRef()
    {
        var svc = CreateService(out var ctx);
        var player = await svc.RegisterAsync("AvatarPlayer", "Google", Guid.NewGuid().ToString());

        // Minimal 1×1 PNG
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        using var stream = new MemoryStream(pngBytes);
        await svc.SetAvatarAsync(player.PlayerId, stream, "image/png");

        // Verify avatar ref was persisted by loading the player from the repository
        var repo = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var updated = await repo.GetByIdAsync(player.PlayerId);
        updated.Should().NotBeNull();
        updated!.Avatar.Should().NotBeNull("avatar should be set after upload");
        updated.Avatar!.ContentType.Should().Be("image/png");
        updated.Avatar.StorageKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SetAvatarAsync_Rejects_InvalidContentType()
    {
        var svc = CreateService();
        var player = await svc.RegisterAsync("AvatarRejectType", "Google", Guid.NewGuid().ToString());

        using var stream = new MemoryStream(new byte[] { 0x00, 0x01 });
        var act = async () => await svc.SetAvatarAsync(player.PlayerId, stream, "application/octet-stream");
        await act.Should().ThrowAsync<ArgumentException>("unsupported content type must be rejected");
    }

    [Fact]
    public async Task SetAvatarAsync_Rejects_OversizedImage()
    {
        var svc = CreateService();
        var player = await svc.RegisterAsync("AvatarRejectSize", "Google", Guid.NewGuid().ToString());

        // Create a seekable stream > 2MB
        var oversized = new byte[3 * 1024 * 1024]; // 3MB
        using var stream = new MemoryStream(oversized);
        var act = async () => await svc.SetAvatarAsync(player.PlayerId, stream, "image/png");
        await act.Should().ThrowAsync<ArgumentException>("images > 2MB must be rejected");
    }

    [Fact]
    public async Task RemoveAvatarAsync_ClearsRefAndDeletesBlob()
    {
        var svc = CreateService(out var ctx);
        var player = await svc.RegisterAsync("RemoveAvatar", "Google", Guid.NewGuid().ToString());

        // Upload an avatar first
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        using var stream = new MemoryStream(pngBytes);
        await svc.SetAvatarAsync(player.PlayerId, stream, "image/png");

        // Now remove it
        await svc.RemoveAvatarAsync(player.PlayerId);

        // Verify via repository that avatar ref is cleared
        var repo = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var updated = await repo.GetByIdAsync(player.PlayerId);
        updated!.Avatar.Should().BeNull("avatar ref should be cleared after removal");
    }
}
