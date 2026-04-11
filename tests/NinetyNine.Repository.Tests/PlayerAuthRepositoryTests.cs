using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NinetyNine.Model;
using NinetyNine.Repository;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Repository.Tests;

/// <summary>
/// Integration tests for the auth-specific methods on <see cref="PlayerRepository"/>
/// (WP-04), and for the auth MongoDB indexes defined in <see cref="BsonConfiguration"/>
/// (WP-01 / WP-04). Uses a shared Testcontainers MongoDB instance.
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class PlayerAuthRepositoryTests(MongoFixture fixture)
{
    private IPlayerRepository CreateRepo() => CreateRepoWithContext(out _);

    /// <summary>
    /// Creates a repository backed by a fresh isolated database.
    /// Also calls <see cref="BsonConfiguration.EnsureAuthIndexesAsync"/> to create the
    /// email unique index and token sparse indexes on the players collection — these
    /// are not wired into <see cref="NinetyNineDbContext"/> yet (production gap; tracked
    /// separately), so tests must call it explicitly.
    /// </summary>
    private IPlayerRepository CreateRepoWithContext(out INinetyNineDbContext ctx)
    {
        ctx = fixture.CreateDbContext();
        // Ensure auth indexes synchronously so tests can rely on them being present.
        BsonConfiguration.EnsureAuthIndexesAsync(ctx.Players).GetAwaiter().GetResult();
        return new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
    }

    /// <summary>
    /// Creates a player with all auth fields populated.
    /// Callers are responsible for lowercasing the email before passing it in,
    /// since the repository contract requires pre-normalized input.
    /// </summary>
    private static Player MakeAuthPlayer(
        string email,
        string displayName,
        string? verificationToken = null,
        string? resetToken = null,
        bool emailVerified = false)
    {
        return new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = displayName,
            EmailAddress = email.ToLowerInvariant(),
            PasswordHash = "hashed",
            EmailVerified = emailVerified,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiresAt = verificationToken is null
                ? null
                : DateTime.UtcNow.AddHours(24),
            PasswordResetToken = resetToken,
            PasswordResetTokenExpiresAt = resetToken is null
                ? null
                : DateTime.UtcNow.AddHours(1),
            FailedLoginAttempts = 0
        };
    }

    // ── GetByEmailAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsPlayer()
    {
        var repo = CreateRepo();
        var player = MakeAuthPlayer("alice@example.com", "Alice");
        await repo.CreateAsync(player);

        var found = await repo.GetByEmailAsync("alice@example.com");

        found.Should().NotBeNull();
        found!.PlayerId.Should().Be(player.PlayerId);
        found.EmailAddress.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task GetByEmailAsync_MixedCaseInput_ReturnsSamePlayerAsLowercase()
    {
        var repo = CreateRepo();
        var player = MakeAuthPlayer("carey@example.com", "Carey");
        await repo.CreateAsync(player);

        // Repository normalizes the lookup to lowercase
        var foundLower = await repo.GetByEmailAsync("carey@example.com");
        var foundMixed = await repo.GetByEmailAsync("Carey@Example.Com");

        foundLower.Should().NotBeNull();
        foundMixed.Should().NotBeNull();
        foundMixed!.PlayerId.Should().Be(foundLower!.PlayerId,
            because: "mixed-case and lowercase lookups must resolve to the same player");
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistentEmail_ReturnsNull()
    {
        var repo = CreateRepo();

        var result = await repo.GetByEmailAsync("nobody_xyz@notfound.example");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByEmailAsync_NullOrEmpty_ReturnsNull(string? email)
    {
        var repo = CreateRepo();

        var result = await repo.GetByEmailAsync(email!);

        result.Should().BeNull("guard clause must return null for null/empty input");
    }

    // ── EmailExistsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task EmailExistsAsync_ExistingEmail_ReturnsTrue()
    {
        var repo = CreateRepo();
        var player = MakeAuthPlayer("exists@example.com", "ExistUser");
        await repo.CreateAsync(player);

        var exists = await repo.EmailExistsAsync("exists@example.com");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task EmailExistsAsync_ExistingEmail_CaseInsensitive_ReturnsTrue()
    {
        var repo = CreateRepo();
        var player = MakeAuthPlayer("mixed@example.com", "MixedUser");
        await repo.CreateAsync(player);

        // Lookup with uppercase — repository normalizes before querying
        var exists = await repo.EmailExistsAsync("MIXED@EXAMPLE.COM");

        exists.Should().BeTrue("lookup must be case-insensitive");
    }

    [Fact]
    public async Task EmailExistsAsync_NonExistentEmail_ReturnsFalse()
    {
        var repo = CreateRepo();

        var exists = await repo.EmailExistsAsync("nothere_xyz@gone.example");

        exists.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmailExistsAsync_NullOrEmpty_ReturnsFalse(string? email)
    {
        var repo = CreateRepo();

        var result = await repo.EmailExistsAsync(email!);

        result.Should().BeFalse("guard clause must return false for null/empty input");
    }

    // ── GetByEmailVerificationTokenAsync ──────────────────────────────────────

    [Fact]
    public async Task GetByEmailVerificationTokenAsync_MatchingToken_ReturnsPlayer()
    {
        var repo = CreateRepo();
        var token = "verify-tok-" + Guid.NewGuid().ToString("N");
        var player = MakeAuthPlayer("verifytoken@example.com", "VerToken", verificationToken: token);
        await repo.CreateAsync(player);

        var found = await repo.GetByEmailVerificationTokenAsync(token);

        found.Should().NotBeNull();
        found!.PlayerId.Should().Be(player.PlayerId);
    }

    [Fact]
    public async Task GetByEmailVerificationTokenAsync_NonExistentToken_ReturnsNull()
    {
        var repo = CreateRepo();

        var result = await repo.GetByEmailVerificationTokenAsync("no-such-token-xyz");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByEmailVerificationTokenAsync_NullOrEmpty_ReturnsNull(string? token)
    {
        var repo = CreateRepo();

        var result = await repo.GetByEmailVerificationTokenAsync(token!);

        result.Should().BeNull("guard clause must short-circuit on null/empty token");
    }

    // ── GetByPasswordResetTokenAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetByPasswordResetTokenAsync_MatchingToken_ReturnsPlayer()
    {
        var repo = CreateRepo();
        var token = "reset-tok-" + Guid.NewGuid().ToString("N");
        var player = MakeAuthPlayer("resettoken@example.com", "ResetToken", resetToken: token);
        await repo.CreateAsync(player);

        var found = await repo.GetByPasswordResetTokenAsync(token);

        found.Should().NotBeNull();
        found!.PlayerId.Should().Be(player.PlayerId);
    }

    [Fact]
    public async Task GetByPasswordResetTokenAsync_NonExistentToken_ReturnsNull()
    {
        var repo = CreateRepo();

        var result = await repo.GetByPasswordResetTokenAsync("no-reset-tok-xyz");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByPasswordResetTokenAsync_NullOrEmpty_ReturnsNull(string? token)
    {
        var repo = CreateRepo();

        var result = await repo.GetByPasswordResetTokenAsync(token!);

        result.Should().BeNull("guard clause must short-circuit on null/empty token");
    }

    // ── Unique index on email ─────────────────────────────────────────────────

    [Fact]
    public async Task UniqueIndex_OnEmail_PreventsDuplicateSameCase()
    {
        var repo = CreateRepo();
        var email = $"dup_{Guid.NewGuid():N}@example.com";
        await repo.CreateAsync(MakeAuthPlayer(email, "UniqueIdx1"));

        var act = async () => await repo.CreateAsync(MakeAuthPlayer(email, "UniqueIdx2"));

        await act.Should().ThrowAsync<Exception>(
            because: "duplicate email violates the unique index on emailAddress");
    }

    [Fact]
    public async Task UniqueIndex_OnEmail_PreventsDuplicateDifferentCase()
    {
        var repo = CreateRepo();
        var baseEmail = $"dupcase_{Guid.NewGuid():N}@example.com";
        // Both players normalized to lowercase before insert — should collide on the index
        await repo.CreateAsync(MakeAuthPlayer(baseEmail.ToLowerInvariant(), "CaseIdx1"));

        var act = async () =>
            await repo.CreateAsync(MakeAuthPlayer(baseEmail.ToUpperInvariant(), "CaseIdx2"));

        // Both are stored as the same lowercased value — unique index catches the duplicate.
        await act.Should().ThrowAsync<Exception>(
            because: "email is lowercased at write time so case variants collide on the index");
    }

    // ── Sparse indexes on tokens ──────────────────────────────────────────────

    [Fact]
    public async Task SparseIndex_OnEmailVerificationToken_AllowsMultipleNullTokens()
    {
        var repo = CreateRepo();
        // Three players, all with null EmailVerificationToken — sparse index must not block this.
        var p1 = MakeAuthPlayer($"sparse1_{Guid.NewGuid():N}@ex.com", "Sparse1");
        var p2 = MakeAuthPlayer($"sparse2_{Guid.NewGuid():N}@ex.com", "Sparse2");
        var p3 = MakeAuthPlayer($"sparse3_{Guid.NewGuid():N}@ex.com", "Sparse3");
        p1.EmailVerificationToken = null;
        p2.EmailVerificationToken = null;
        p3.EmailVerificationToken = null;

        var act = async () =>
        {
            await repo.CreateAsync(p1);
            await repo.CreateAsync(p2);
            await repo.CreateAsync(p3);
        };

        await act.Should().NotThrowAsync(
            because: "a sparse index skips null values, so multiple nulls must be allowed");
    }

    [Fact]
    public async Task SparseIndex_OnPasswordResetToken_AllowsMultipleNullTokens()
    {
        var repo = CreateRepo();
        var p1 = MakeAuthPlayer($"rsparse1_{Guid.NewGuid():N}@ex.com", "Rsparse1");
        var p2 = MakeAuthPlayer($"rsparse2_{Guid.NewGuid():N}@ex.com", "Rsparse2");
        p1.PasswordResetToken = null;
        p2.PasswordResetToken = null;

        var act = async () =>
        {
            await repo.CreateAsync(p1);
            await repo.CreateAsync(p2);
        };

        await act.Should().NotThrowAsync(
            because: "sparse index on passwordResetToken must allow multiple null values");
    }

    // ── Auth fields round-trip through MongoDB serialization ─────────────────

    [Fact]
    public async Task CreateAsync_AuthFields_SurviveMongoRoundTrip()
    {
        var repo = CreateRepo();
        var verToken = "ver-" + Guid.NewGuid().ToString("N");
        var rstToken = "rst-" + Guid.NewGuid().ToString("N");
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var original = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "RoundTripAuth",
            EmailAddress = $"roundtrip_{Guid.NewGuid():N}@example.com",
            PasswordHash = "pbkdf2hash==",
            EmailVerified = false,
            EmailVerificationToken = verToken,
            EmailVerificationTokenExpiresAt = now.AddHours(24),
            PasswordResetToken = rstToken,
            PasswordResetTokenExpiresAt = now.AddHours(1),
            LastLoginAt = now,
            FailedLoginAttempts = 2,
            LockedOutUntil = now.AddMinutes(15)
        };

        await repo.CreateAsync(original);
        var retrieved = await repo.GetByIdAsync(original.PlayerId);

        retrieved.Should().NotBeNull();
        retrieved!.EmailAddress.Should().Be(original.EmailAddress);
        retrieved.PasswordHash.Should().Be("pbkdf2hash==");
        retrieved.EmailVerified.Should().BeFalse();
        retrieved.EmailVerificationToken.Should().Be(verToken);
        retrieved.EmailVerificationTokenExpiresAt.Should().Be(now.AddHours(24));
        retrieved.PasswordResetToken.Should().Be(rstToken);
        retrieved.PasswordResetTokenExpiresAt.Should().Be(now.AddHours(1));
        retrieved.LastLoginAt.Should().Be(now);
        retrieved.FailedLoginAttempts.Should().Be(2);
        retrieved.LockedOutUntil.Should().Be(now.AddMinutes(15));
    }

    [Fact]
    public async Task CreateAsync_NullTokensAndZeroAttempts_SurviveMongoRoundTrip()
    {
        var repo = CreateRepo();
        var original = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "NullTokensRT",
            EmailAddress = $"nulltok_{Guid.NewGuid():N}@example.com",
            PasswordHash = "",
            EmailVerified = false,
            EmailVerificationToken = null,
            EmailVerificationTokenExpiresAt = null,
            PasswordResetToken = null,
            PasswordResetTokenExpiresAt = null,
            LastLoginAt = null,
            FailedLoginAttempts = 0,
            LockedOutUntil = null
        };

        await repo.CreateAsync(original);
        var retrieved = await repo.GetByIdAsync(original.PlayerId);

        retrieved.Should().NotBeNull();
        retrieved!.EmailVerificationToken.Should().BeNull();
        retrieved.EmailVerificationTokenExpiresAt.Should().BeNull();
        retrieved.PasswordResetToken.Should().BeNull();
        retrieved.PasswordResetTokenExpiresAt.Should().BeNull();
        retrieved.LastLoginAt.Should().BeNull();
        retrieved.FailedLoginAttempts.Should().Be(0);
        retrieved.LockedOutUntil.Should().BeNull();
    }
}
