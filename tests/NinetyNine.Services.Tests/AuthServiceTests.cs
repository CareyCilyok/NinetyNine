using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services.Auth;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Integration tests for <see cref="AuthService"/> using a real MongoDB Testcontainer,
/// real <see cref="PlayerRepository"/>, a real <see cref="PasswordHasher{TUser}"/>,
/// and the hand-written <see cref="TestEmailSender"/>.
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class AuthServiceTests(MongoFixture fixture)
{
    private const string VerifyBase = "https://app.example.com";
    private const string ResetBase  = "https://app.example.com";
    private const string ValidPassword = "Test1234!abc";

    // ── Helper: build a fresh AuthService backed by an isolated DB ─────────────

    private (AuthService svc, IPlayerRepository repo, TestEmailSender email) CreateSut()
    {
        var ctx    = fixture.CreateDbContext();
        var repo   = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var email  = new TestEmailSender();
        var hasher = new PasswordHasher<Player>();
        var svc    = new AuthService(repo, email, hasher, NullLogger<AuthService>.Instance);
        return (svc, repo, email);
    }

    private static string UniqueEmail(string prefix = "user")
        => $"{prefix}_{Guid.NewGuid():N}@example.com";

    private static string UniqueName(string prefix = "Player")
        => $"{prefix}{Guid.NewGuid():N}"[..16]; // max 16 chars, alphanumeric

    // ── Register flow ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidInput_CreatesPlayerWithHashedPasswordAndUnverifiedEmail()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail();
        var name  = UniqueName();

        var result = await svc.RegisterAsync(email, name, ValidPassword, ValidPassword, VerifyBase);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.EmailAddress.Should().Be(email.ToLowerInvariant());
        result.Value.DisplayName.Should().Be(name);
        result.Value.EmailVerified.Should().BeFalse("email is not yet verified at registration");
        result.Value.PasswordHash.Should().NotBeNullOrEmpty("password must be hashed, not stored plain");
        result.Value.PasswordHash.Should().NotBe(ValidPassword, "stored value must be a hash, not the plain password");
        result.Value.EmailVerificationToken.Should().NotBeNullOrEmpty();
        result.Value.EmailVerificationTokenExpiresAt.Should().NotBeNull();
        result.Value.EmailVerificationTokenExpiresAt!.Value
            .Should().BeAfter(DateTime.UtcNow, "token should expire in the future");
    }

    [Fact]
    public async Task Register_ValidInput_SendsVerificationEmail()
    {
        var (svc, _, email) = CreateSut();
        var addr = UniqueEmail("verify");
        var name = UniqueName("Verif");

        var result = await svc.RegisterAsync(addr, name, ValidPassword, ValidPassword, VerifyBase);

        result.Success.Should().BeTrue();
        email.SentEmails.Should().HaveCount(1);
        var sent = email.SentEmails[0];
        sent.Kind.Should().Be("Verification");
        sent.To.Should().Be(addr.ToLowerInvariant());
        sent.DisplayName.Should().Be(name);
        sent.Url.Should().Contain("verify-email?token=");
        // Token from the registered player must appear in the URL.
        var token = result.Value!.EmailVerificationToken!;
        sent.Url.Should().Contain(Uri.EscapeDataString(token));
    }

    [Fact]
    public async Task Register_DuplicateEmail_SameCase_ReturnsGenericError()
    {
        var (svc, _, _) = CreateSut();
        var email = UniqueEmail("dup");
        var name1 = UniqueName("Dup1");
        var name2 = UniqueName("Dup2");

        await svc.RegisterAsync(email, name1, ValidPassword, ValidPassword, VerifyBase);
        var result = await svc.RegisterAsync(email, name2, ValidPassword, ValidPassword, VerifyBase);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Email or display name is invalid.",
            because: "error message must not reveal which field collided (anti-enumeration)");
    }

    [Fact]
    public async Task Register_DuplicateEmail_DifferentCase_ReturnsGenericError()
    {
        var (svc, _, _) = CreateSut();
        var email = UniqueEmail("case");
        var name1 = UniqueName("Case1");
        var name2 = UniqueName("Case2");

        // First registration with lowercased email
        await svc.RegisterAsync(email.ToLowerInvariant(), name1, ValidPassword, ValidPassword, VerifyBase);

        // Second registration with uppercased email — should be treated as the same address
        var result = await svc.RegisterAsync(email.ToUpperInvariant(), name2, ValidPassword, ValidPassword, VerifyBase);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Email or display name is invalid.");
    }

    [Fact]
    public async Task Register_DuplicateDisplayName_ReturnsGenericError()
    {
        var (svc, _, _) = CreateSut();
        var email1 = UniqueEmail("e1");
        var email2 = UniqueEmail("e2");
        var name   = UniqueName("Shared");

        await svc.RegisterAsync(email1, name, ValidPassword, ValidPassword, VerifyBase);
        var result = await svc.RegisterAsync(email2, name, ValidPassword, ValidPassword, VerifyBase);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Email or display name is invalid.",
            because: "must not reveal which field collided");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("")]
    public async Task Register_InvalidEmailFormat_ReturnsError(string badEmail)
    {
        var (svc, _, _) = CreateSut();
        var result = await svc.RegisterAsync(badEmail, UniqueName(), ValidPassword, ValidPassword, VerifyBase);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Email or display name is invalid.");
    }

    [Theory]
    [InlineData("Short1!")]           // < 10 chars
    [InlineData("alllowercase1!ab")]  // missing uppercase
    [InlineData("ALLUPPERCASE1!AB")]  // missing lowercase
    [InlineData("NoDigitsHere!!aa")]  // missing digit
    [InlineData("NoSymbol1234abc")]   // missing symbol from allowed set
    public async Task Register_WeakPassword_ReturnsPasswordError(string weakPassword)
    {
        var (svc, _, _) = CreateSut();
        var result = await svc.RegisterAsync(
            UniqueEmail(), UniqueName(), weakPassword, weakPassword, VerifyBase);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Password requirements:");
    }

    [Fact]
    public async Task Register_PasswordConfirmMismatch_ReturnsError()
    {
        var (svc, _, _) = CreateSut();
        var result = await svc.RegisterAsync(
            UniqueEmail(), UniqueName(), ValidPassword, "DifferentPass1!", VerifyBase);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Passwords do not match.");
    }

    // ── Login flow ─────────────────────────────────────────────────────────────

    private async Task<Player> RegisterAndVerify(
        AuthService svc,
        IPlayerRepository repo,
        string email,
        string name,
        string password = ValidPassword)
    {
        var regResult = await svc.RegisterAsync(email, name, password, password, VerifyBase);
        regResult.Success.Should().BeTrue($"registration of {email} must succeed");

        var player = regResult.Value!;
        // Directly verify by updating the player document so we can test login flows
        // without needing to invoke VerifyEmailAsync (tested separately below).
        player.EmailVerified = true;
        player.EmailVerificationToken = null;
        player.EmailVerificationTokenExpiresAt = null;
        await repo.UpdateAsync(player);
        return player;
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsPlayer()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("login");
        var name  = UniqueName("Login");
        await RegisterAndVerify(svc, repo, email, name);

        var result = await svc.LoginAsync(email, ValidPassword);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.EmailAddress.Should().Be(email.ToLowerInvariant());
    }

    [Fact]
    public async Task Login_ValidCredentials_SetsLastLoginAt()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("lastlogin");
        await RegisterAndVerify(svc, repo, email, UniqueName("LL"));
        var before = DateTime.UtcNow;

        await svc.LoginAsync(email, ValidPassword);

        var updated = await repo.GetByEmailAsync(email);
        updated!.LastLoginAt.Should().NotBeNull();
        updated.LastLoginAt!.Value.Should().BeOnOrAfter(before - TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Login_ValidCredentials_ResetsFailedLoginAttempts()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("reset");
        var player = await RegisterAndVerify(svc, repo, email, UniqueName("Rst"));

        // Manually set some prior failures
        player.FailedLoginAttempts = 3;
        await repo.UpdateAsync(player);

        await svc.LoginAsync(email, ValidPassword);

        var updated = await repo.GetByEmailAsync(email);
        updated!.FailedLoginAttempts.Should().Be(0, "successful login resets the failure counter");
    }

    [Fact]
    public async Task Login_InvalidPassword_IncrementsFailedLoginAttempts()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("fail");
        await RegisterAndVerify(svc, repo, email, UniqueName("Fail"));

        var result = await svc.LoginAsync(email, "WrongPass1!");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials.");
        var updated = await repo.GetByEmailAsync(email);
        updated!.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Login_FiveFailedAttempts_LocksAccountAndResetsCounter()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("lock");
        await RegisterAndVerify(svc, repo, email, UniqueName("Lock"));

        // Submit 5 bad passwords consecutively
        for (int i = 0; i < 5; i++)
            await svc.LoginAsync(email, "BadPass1!");

        var player = await repo.GetByEmailAsync(email);
        // After lockout the counter resets to 0 so the next window starts fresh
        player!.LockedOutUntil.Should().NotBeNull("account must be locked after 5 failures");
        player.LockedOutUntil!.Value.Should().BeAfter(DateTime.UtcNow - TimeSpan.FromSeconds(5));
        player.FailedLoginAttempts.Should().Be(0, "counter is reset to 0 when lockout is applied");
    }

    [Fact]
    public async Task Login_LockedAccount_RejectsEvenCorrectPassword()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("locked");
        var player = await RegisterAndVerify(svc, repo, email, UniqueName("Locked"));

        // Manually lock the account
        player.LockedOutUntil = DateTime.UtcNow.AddMinutes(15);
        await repo.UpdateAsync(player);

        var result = await svc.LoginAsync(email, ValidPassword);

        result.Success.Should().BeFalse();
        // Spec: lockout message must NOT say "Invalid credentials." — it says "Account locked."
        result.ErrorMessage.Should().Contain("locked");
    }

    [Fact]
    public async Task Login_UnverifiedEmail_ReturnsVerificationMessage()
    {
        var (svc, _, _) = CreateSut();
        var email = UniqueEmail("unverified");
        // Register without verifying — EmailVerified stays false
        await svc.RegisterAsync(email, UniqueName("Unver"), ValidPassword, ValidPassword, VerifyBase);

        var result = await svc.LoginAsync(email, ValidPassword);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Please verify your email before signing in.",
            because: "per spec §7.5 this is an acceptable enumeration leak for UX reasons");
    }

    [Fact]
    public async Task Login_NonExistentEmail_ReturnsGenericError_WithoutThrowing()
    {
        var (svc, _, _) = CreateSut();

        // The dummy-hash path must not throw even for a completely unknown email
        var act = async () => await svc.LoginAsync("nobody_xyz@notreal.example", ValidPassword);
        var result = await act.Should().NotThrowAsync();

        result.Subject.Success.Should().BeFalse();
        result.Subject.ErrorMessage.Should().Be("Invalid credentials.");
    }

    // ── VerifyEmailAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_ValidToken_SetsEmailVerifiedAndClearsToken()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("verifyok");
        var regResult = await svc.RegisterAsync(
            email, UniqueName("VerOk"), ValidPassword, ValidPassword, VerifyBase);
        var token = regResult.Value!.EmailVerificationToken!;

        var result = await svc.VerifyEmailAsync(token);

        result.Success.Should().BeTrue();

        var player = await repo.GetByEmailAsync(email);
        player!.EmailVerified.Should().BeTrue();
        player.EmailVerificationToken.Should().BeNull("token must be cleared after use");
        player.EmailVerificationTokenExpiresAt.Should().BeNull("expiry must be cleared after use");
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_ReturnsFail()
    {
        var (svc, _, _) = CreateSut();

        var result = await svc.VerifyEmailAsync("this-token-does-not-exist");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid or expired verification link.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyEmail_EmptyOrWhitespaceToken_ReturnsFail(string token)
    {
        var (svc, _, _) = CreateSut();

        var result = await svc.VerifyEmailAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid or expired verification link.");
    }

    [Fact]
    public async Task VerifyEmail_ExpiredToken_ReturnsFail()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("expiredver");
        var regResult = await svc.RegisterAsync(
            email, UniqueName("Expired"), ValidPassword, ValidPassword, VerifyBase);
        var player = regResult.Value!;

        // Directly backdating the expiry simulates an expired token without sleeping.
        player.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(-1);
        await repo.UpdateAsync(player);

        var result = await svc.VerifyEmailAsync(player.EmailVerificationToken!);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid or expired verification link.");
    }

    // ── Password reset flow ────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_ValidToken_UpdatesPasswordHashAndClearsToken()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("resetok");
        var player = await RegisterAndVerify(svc, repo, email, UniqueName("ResetOk"));

        // Trigger forgot-password to get a reset token
        await svc.ForgotPasswordAsync(email, ResetBase);
        var withToken = await repo.GetByEmailAsync(email);
        withToken!.PasswordResetToken.Should().NotBeNull();
        var resetToken = withToken.PasswordResetToken!;

        var newPassword = "NewSecure1!ab";
        var result = await svc.ResetPasswordAsync(resetToken, newPassword, newPassword);

        result.Success.Should().BeTrue();

        var updated = await repo.GetByEmailAsync(email);
        updated!.PasswordResetToken.Should().BeNull("token cleared after use");
        updated.PasswordResetTokenExpiresAt.Should().BeNull("expiry cleared after use");

        // Verify the new password works by logging in with it
        var loginResult = await svc.LoginAsync(email, newPassword);
        loginResult.Success.Should().BeTrue("new password should authenticate successfully");
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_ReturnsFail()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("expiredreset");
        var player = await RegisterAndVerify(svc, repo, email, UniqueName("ExpReset"));

        // Inject a reset token with a past expiry directly into the document
        player.PasswordResetToken = "expired-reset-token";
        player.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(-1);
        await repo.UpdateAsync(player);

        var result = await svc.ResetPasswordAsync("expired-reset-token", ValidPassword, ValidPassword);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid or expired reset link.");
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsFail()
    {
        var (svc, _, _) = CreateSut();

        var result = await svc.ResetPasswordAsync("no-such-token", ValidPassword, ValidPassword);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid or expired reset link.");
    }

    [Fact]
    public async Task ResetPassword_WeakNewPassword_ReturnsPasswordError()
    {
        var (svc, repo, _) = CreateSut();
        var email = UniqueEmail("weakreset");
        var player = await RegisterAndVerify(svc, repo, email, UniqueName("WeakR"));

        // Inject a valid reset token
        player.PasswordResetToken = "valid-reset-token";
        player.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        await repo.UpdateAsync(player);

        // Attempt to reset with a weak password
        var result = await svc.ResetPasswordAsync("valid-reset-token", "weak", "weak");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Password requirements:");
    }

    // ── Resend verification ────────────────────────────────────────────────────

    [Fact]
    public async Task ResendVerification_UnverifiedAccount_SendsNewTokenAndEmail()
    {
        var (svc, repo, emailSender) = CreateSut();
        var email = UniqueEmail("resend");
        var regResult = await svc.RegisterAsync(
            email, UniqueName("Resend"), ValidPassword, ValidPassword, VerifyBase);
        var oldToken = regResult.Value!.EmailVerificationToken!;
        emailSender.Clear();

        var result = await svc.ResendVerificationAsync(email, VerifyBase);

        result.Success.Should().BeTrue();
        emailSender.SentEmails.Should().HaveCount(1, "exactly one email must be sent on resend");
        emailSender.SentEmails[0].Kind.Should().Be("Verification");

        var player = await repo.GetByEmailAsync(email);
        player!.EmailVerificationToken.Should().NotBe(oldToken, "new token must replace the old one");
        player.EmailVerificationTokenExpiresAt.Should().BeAfter(DateTime.UtcNow - TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ResendVerification_AlreadyVerifiedAccount_DoesNotSendEmail()
    {
        var (svc, repo, emailSender) = CreateSut();
        var email = UniqueEmail("alreadyver");
        var player = await RegisterAndVerify(svc, repo, email, UniqueName("AlrVer"));
        emailSender.Clear();

        var result = await svc.ResendVerificationAsync(email, VerifyBase);

        result.Success.Should().BeTrue("silently succeeds — no enumeration");
        emailSender.SentEmails.Should().BeEmpty("verified accounts get no re-verification email");
    }

    [Fact]
    public async Task ResendVerification_NonExistentEmail_SilentlySucceeds()
    {
        var (svc, _, emailSender) = CreateSut();

        var result = await svc.ResendVerificationAsync("nobody@notexist.example", VerifyBase);

        result.Success.Should().BeTrue("must not reveal whether the address exists");
        emailSender.SentEmails.Should().BeEmpty();
    }

    // ── Forgot password ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_ExistingEmail_GeneratesResetTokenAndSendsEmail()
    {
        var (svc, repo, emailSender) = CreateSut();
        var email = UniqueEmail("forgot");
        await RegisterAndVerify(svc, repo, email, UniqueName("Forgot"));
        emailSender.Clear();

        var result = await svc.ForgotPasswordAsync(email, ResetBase);

        result.Success.Should().BeTrue();
        emailSender.SentEmails.Should().HaveCount(1);
        emailSender.SentEmails[0].Kind.Should().Be("PasswordReset");
        emailSender.SentEmails[0].To.Should().Be(email.ToLowerInvariant());
        emailSender.SentEmails[0].Url.Should().Contain("reset-password?token=");

        var player = await repo.GetByEmailAsync(email);
        player!.PasswordResetToken.Should().NotBeNullOrEmpty();
        player.PasswordResetTokenExpiresAt.Should().NotBeNull();
        player.PasswordResetTokenExpiresAt!.Value.Should().BeAfter(DateTime.UtcNow - TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_SilentlySucceeds_NoEmailSent()
    {
        var (svc, _, emailSender) = CreateSut();

        var result = await svc.ForgotPasswordAsync("ghost@nowhere.example", ResetBase);

        result.Success.Should().BeTrue("must not reveal whether the address exists");
        emailSender.SentEmails.Should().BeEmpty("no email for non-existent accounts");
    }
}
