using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

/// <summary>
/// Basic construction and property tests for Model entities to ensure
/// default values, Guid generation, and initialization are correct.
/// These provide line coverage for otherwise-zero-covered data classes.
/// </summary>
public class ModelEntityTests
{
    // ── Player ────────────────────────────────────────────────────────────────

    [Fact]
    public void Player_Defaults_HaveCorrectValues()
    {
        var player = new Player();
        player.PlayerId.Should().NotBeEmpty("PlayerId auto-generated");
        player.DisplayName.Should().Be("");
        player.EmailAddress.Should().Be("");
        player.PhoneNumber.Should().BeNull();
        player.FirstName.Should().BeNull();
        player.MiddleName.Should().BeNull();
        player.LastName.Should().BeNull();
        player.Avatar.Should().BeNull();
        player.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Player_Visibility_Defaults()
    {
        var player = new Player();
        player.Visibility.Should().NotBeNull();
        player.Visibility.EmailAudience.Should().Be(Audience.Private);
        player.Visibility.PhoneAudience.Should().Be(Audience.Private);
        player.Visibility.RealNameAudience.Should().Be(Audience.Private);
        player.Visibility.AvatarAudience.Should().Be(Audience.Public, "avatar is visible by default");
    }

    [Fact]
    public void Player_CanSetAllProperties()
    {
        var id = Guid.NewGuid();
        var player = new Player
        {
            PlayerId = id,
            DisplayName = "TestUser",
            EmailAddress = "test@example.com",
            PhoneNumber = "555-0000",
            FirstName = "First",
            MiddleName = "Mid",
            LastName = "Last",
            Visibility = new ProfileVisibility { RealNameAudience = Audience.Friends },
            Avatar = new AvatarRef { StorageKey = "key123", ContentType = "image/png" }
        };
        player.PlayerId.Should().Be(id);
        player.DisplayName.Should().Be("TestUser");
        player.EmailAddress.Should().Be("test@example.com");
        player.PhoneNumber.Should().Be("555-0000");
        player.FirstName.Should().Be("First");
        player.MiddleName.Should().Be("Mid");
        player.LastName.Should().Be("Last");
        player.Visibility.RealNameAudience.Should().Be(Audience.Friends);
        player.Avatar!.StorageKey.Should().Be("key123");
    }

    // ── ProfileVisibility ─────────────────────────────────────────────────────

    [Fact]
    public void ProfileVisibility_CanSetAllFlags()
    {
        var vis = new ProfileVisibility
        {
            EmailAudience = Audience.Friends,
            PhoneAudience = Audience.Friends,
            RealNameAudience = Audience.Friends,
            AvatarAudience = Audience.Private
        };
        vis.EmailAudience.Should().Be(Audience.Friends);
        vis.PhoneAudience.Should().Be(Audience.Friends);
        vis.RealNameAudience.Should().Be(Audience.Friends);
        vis.AvatarAudience.Should().Be(Audience.Private);
    }

    // ── AvatarRef ─────────────────────────────────────────────────────────────

    [Fact]
    public void AvatarRef_Defaults()
    {
        var avatar = new AvatarRef();
        avatar.StorageKey.Should().Be("");
        avatar.ContentType.Should().Be("");
        avatar.WidthPx.Should().Be(0);
        avatar.HeightPx.Should().Be(0);
        avatar.SizeBytes.Should().Be(0);
        avatar.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AvatarRef_CanSetAllProperties()
    {
        var avatar = new AvatarRef
        {
            StorageKey = "507f1f77bcf86cd799439011",
            ContentType = "image/webp",
            WidthPx = 256,
            HeightPx = 256,
            SizeBytes = 8192,
            UploadedAt = DateTime.UtcNow
        };
        avatar.StorageKey.Should().Be("507f1f77bcf86cd799439011");
        avatar.ContentType.Should().Be("image/webp");
        avatar.WidthPx.Should().Be(256);
        avatar.HeightPx.Should().Be(256);
        avatar.SizeBytes.Should().Be(8192);
    }

    // ── Venue ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Venue_Defaults()
    {
        var venue = new Venue();
        venue.VenueId.Should().NotBeEmpty();
        venue.Name.Should().Be("");
        venue.Address.Should().Be("");
        venue.PhoneNumber.Should().Be("");
        venue.Private.Should().BeFalse();
    }

    [Fact]
    public void Venue_CanSetAllProperties()
    {
        var id = Guid.NewGuid();
        var venue = new Venue
        {
            VenueId = id,
            Name = "Pool Palace",
            Address = "123 Main St",
            PhoneNumber = "555-9100",
            Private = true
        };
        venue.VenueId.Should().Be(id);
        venue.Name.Should().Be("Pool Palace");
        venue.Address.Should().Be("123 Main St");
        venue.PhoneNumber.Should().Be("555-9100");
        venue.Private.Should().BeTrue();
    }

    // ── Enum coverage ─────────────────────────────────────────────────────────

    [Fact]
    public void GameState_Enum_HasExpectedValues()
    {
        GameState.NotStarted.Should().Be((GameState)0);
        GameState.InProgress.Should().Be((GameState)1);
        GameState.Completed.Should().Be((GameState)2);
        GameState.Paused.Should().Be((GameState)3);
    }

    [Fact]
    public void TableSize_Enum_HasExpectedValues()
    {
        TableSize.Unknown.Should().Be((TableSize)0);
        TableSize.SixFoot.Should().Be((TableSize)6);
        TableSize.SevenFoot.Should().Be((TableSize)7);
        TableSize.NineFoot.Should().Be((TableSize)9);
        TableSize.TenFoot.Should().Be((TableSize)10);
    }
}
