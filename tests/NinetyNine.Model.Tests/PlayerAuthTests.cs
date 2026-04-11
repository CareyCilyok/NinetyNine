using System.Text.Json;
using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

/// <summary>
/// Unit tests for the authentication-related properties added to <see cref="Player"/>
/// in WP-01. These are pure in-memory tests — no database or external dependencies.
/// </summary>
public class PlayerAuthTests
{
    // ── Default values ─────────────────────────────────────────────────────────

    [Fact]
    public void Player_AuthDefaults_EmailVerifiedIsFalse()
    {
        var player = new Player();
        player.EmailVerified.Should().BeFalse("new players have not verified their email yet");
    }

    [Fact]
    public void Player_AuthDefaults_FailedLoginAttemptsIsZero()
    {
        var player = new Player();
        player.FailedLoginAttempts.Should().Be(0, "no failed attempts at construction");
    }

    [Fact]
    public void Player_AuthDefaults_NullableTokenFieldsAreNull()
    {
        var player = new Player();
        player.EmailVerificationToken.Should().BeNull();
        player.EmailVerificationTokenExpiresAt.Should().BeNull();
        player.PasswordResetToken.Should().BeNull();
        player.PasswordResetTokenExpiresAt.Should().BeNull();
        player.LastLoginAt.Should().BeNull();
        player.LockedOutUntil.Should().BeNull();
    }

    [Fact]
    public void Player_AuthDefaults_PasswordHashIsEmptyString()
    {
        var player = new Player();
        player.PasswordHash.Should().Be("", "PasswordHash initializes to empty string, not null");
    }

    [Fact]
    public void Player_EmailAddress_InitializesToEmptyString()
    {
        var player = new Player();
        // EmailAddress is non-nullable string — verifies the = "" initializer is present.
        player.EmailAddress.Should().Be("");
        // Compile-time guard: assigning to a non-nullable string should not require ! operator.
        string email = player.EmailAddress;
        email.Should().Be("");
    }

    // ── Nullable reference annotation correctness ──────────────────────────────

    [Fact]
    public void Player_EmailVerificationToken_CanBeNull_WithoutNRE()
    {
        var player = new Player();
        // Pattern-matching on a null nullable token must not throw.
        var isNull = player.EmailVerificationToken is null;
        isNull.Should().BeTrue();
    }

    [Fact]
    public void Player_PasswordResetToken_CanBeNull_WithoutNRE()
    {
        var player = new Player();
        var isNull = player.PasswordResetToken is null;
        isNull.Should().BeTrue();
    }

    // ── Fully-populated construction ──────────────────────────────────────────

    [Fact]
    public void Player_AllAuthFields_CanBeSet()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var player = new Player
        {
            PlayerId = id,
            DisplayName = "AuthPlayer",
            EmailAddress = "auth@example.com",
            PasswordHash = "hashed_value",
            EmailVerified = true,
            EmailVerificationToken = "tok1",
            EmailVerificationTokenExpiresAt = now.AddHours(24),
            PasswordResetToken = "tok2",
            PasswordResetTokenExpiresAt = now.AddHours(1),
            LastLoginAt = now,
            FailedLoginAttempts = 3,
            LockedOutUntil = now.AddMinutes(15)
        };

        player.PlayerId.Should().Be(id);
        player.EmailAddress.Should().Be("auth@example.com");
        player.PasswordHash.Should().Be("hashed_value");
        player.EmailVerified.Should().BeTrue();
        player.EmailVerificationToken.Should().Be("tok1");
        player.EmailVerificationTokenExpiresAt.Should().BeCloseTo(now.AddHours(24), TimeSpan.FromSeconds(1));
        player.PasswordResetToken.Should().Be("tok2");
        player.PasswordResetTokenExpiresAt.Should().BeCloseTo(now.AddHours(1), TimeSpan.FromSeconds(1));
        player.LastLoginAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        player.FailedLoginAttempts.Should().Be(3);
        player.LockedOutUntil.Should().BeCloseTo(now.AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    // ── JSON serialization round-trip ─────────────────────────────────────────

    [Fact]
    public void Player_WithDefaultAuthFields_RoundTripsJsonCorrectly()
    {
        var original = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "RoundTripUser",
            EmailAddress = "rt@example.com",
            PasswordHash = "somehash",
            EmailVerified = false,
            FailedLoginAttempts = 0
            // All nullable tokens remain null
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Player>(json);

        restored.Should().NotBeNull();
        restored!.PlayerId.Should().Be(original.PlayerId);
        restored.EmailAddress.Should().Be("rt@example.com");
        restored.PasswordHash.Should().Be("somehash");
        restored.EmailVerified.Should().BeFalse();
        restored.FailedLoginAttempts.Should().Be(0);
        restored.EmailVerificationToken.Should().BeNull();
        restored.EmailVerificationTokenExpiresAt.Should().BeNull();
        restored.PasswordResetToken.Should().BeNull();
        restored.PasswordResetTokenExpiresAt.Should().BeNull();
        restored.LastLoginAt.Should().BeNull();
        restored.LockedOutUntil.Should().BeNull();
    }

    [Fact]
    public void Player_WithAllAuthFieldsSet_RoundTripsJsonCorrectly()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var original = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "FullAuthPlayer",
            EmailAddress = "full@example.com",
            PasswordHash = "pbkdf2hash==",
            EmailVerified = true,
            EmailVerificationToken = "verif-token-abc123",
            EmailVerificationTokenExpiresAt = now.AddHours(24),
            PasswordResetToken = "reset-token-xyz789",
            PasswordResetTokenExpiresAt = now.AddHours(1),
            LastLoginAt = now,
            FailedLoginAttempts = 2,
            LockedOutUntil = now.AddMinutes(15)
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Player>(json);

        restored.Should().NotBeNull();
        restored!.EmailVerified.Should().BeTrue();
        restored.EmailVerificationToken.Should().Be("verif-token-abc123");
        restored.EmailVerificationTokenExpiresAt.Should().Be(now.AddHours(24));
        restored.PasswordResetToken.Should().Be("reset-token-xyz789");
        restored.PasswordResetTokenExpiresAt.Should().Be(now.AddHours(1));
        restored.LastLoginAt.Should().Be(now);
        restored.FailedLoginAttempts.Should().Be(2);
        restored.LockedOutUntil.Should().Be(now.AddMinutes(15));
    }

    [Fact]
    public void Player_BothTokensSet_SerializesWithoutError()
    {
        // Edge case: rapid reset-then-verify scenario where both tokens coexist briefly.
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "BothTokens",
            EmailAddress = "both@example.com",
            EmailVerificationToken = "verify-tok",
            EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24),
            PasswordResetToken = "reset-tok",
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            EmailVerified = false
        };

        var act = () => JsonSerializer.Serialize(player);
        act.Should().NotThrow("both tokens coexisting is a valid transient state");

        var json = act();
        json.Should().Contain("verify-tok").And.Contain("reset-tok");
    }
}
