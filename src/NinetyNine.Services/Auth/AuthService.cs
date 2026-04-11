using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;


namespace NinetyNine.Services.Auth;

/// <summary>
/// Email/password-based authentication service.
/// Implements registration, login, email verification, and password reset flows
/// with security controls against user enumeration, timing attacks, and brute-force.
/// </summary>
public sealed partial class AuthService : IAuthService
{
    // DisplayName constraints — must match PlayerService rules exactly.
    private const int DisplayNameMinLength = 2;
    private const int DisplayNameMaxLength = 32;

    // Lockout policy constants
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Token expiry constants
    private static readonly TimeSpan VerificationTokenExpiry = TimeSpan.FromHours(24);
    private static readonly TimeSpan PasswordResetTokenExpiry = TimeSpan.FromHours(1);

    // Pre-computed dummy hash used to keep timing constant when email is not found.
    // This is a valid PBKDF2 hash of the string "DUMMY_CONSTANT_PASSWORD_FOR_TIMING" —
    // computing a verify against it takes the same time as a real failed verification.
    private static readonly string _dummyHash;

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]{2,32}$", RegexOptions.Compiled)]
    private static partial Regex DisplayNameRegex();

    private readonly IPlayerRepository _playerRepository;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordHasher<Player> _passwordHasher;
    private readonly ILogger<AuthService> _logger;
    private readonly TimeProvider _timeProvider;

    static AuthService()
    {
        // Generate the dummy hash once at class initialization time.
        // This avoids the first-call timing anomaly.
        var hasher = new PasswordHasher<Player>();
        _dummyHash = hasher.HashPassword(new Player(), "DUMMY_CONSTANT_PASSWORD_FOR_TIMING_12345!");
    }

    /// <summary>
    /// Initializes a new <see cref="AuthService"/>.
    /// </summary>
    /// <param name="playerRepository">Player data access layer.</param>
    /// <param name="emailSender">Transactional email delivery abstraction.</param>
    /// <param name="passwordHasher">PBKDF2 password hasher from ASP.NET Core Identity.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="timeProvider">Time provider for testability. Defaults to <see cref="TimeProvider.System"/>.</param>
    public AuthService(
        IPlayerRepository playerRepository,
        IEmailSender emailSender,
        IPasswordHasher<Player> passwordHasher,
        ILogger<AuthService> logger,
        TimeProvider? timeProvider = null)
    {
        _playerRepository = playerRepository;
        _emailSender = emailSender;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<AuthResult<Player>> RegisterAsync(
        string email,
        string displayName,
        string password,
        string confirmPassword,
        string verifyUrlBase,
        CancellationToken ct = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? "";

        // Validate email format — return generic message to prevent enumeration.
        if (!new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            _logger.LogWarning("Registration rejected: invalid email format (normalized).");
            return AuthResult<Player>.Fail("Email or display name is invalid.");
        }

        // Validate display name format.
        if (string.IsNullOrWhiteSpace(displayName) || !DisplayNameRegex().IsMatch(displayName))
        {
            _logger.LogWarning("Registration rejected: display name failed format validation.");
            return AuthResult<Player>.Fail("Email or display name is invalid.");
        }

        // Validate passwords match before hitting the complexity rules.
        if (password != confirmPassword)
            return AuthResult<Player>.Fail("Passwords do not match.");

        // Validate password complexity.
        var passwordErrors = PasswordValidator.Validate(password);
        if (passwordErrors.Count > 0)
        {
            var combined = string.Join(" ", passwordErrors);
            return AuthResult<Player>.Fail($"Password requirements: {combined}");
        }

        // Check uniqueness — return the same generic message whether email or name is taken.
        // This prevents enumeration of existing accounts.
        var emailTaken = await _playerRepository.EmailExistsAsync(normalizedEmail, ct);
        var nameTaken = await _playerRepository.DisplayNameExistsAsync(displayName, ct);
        if (emailTaken || nameTaken)
        {
            _logger.LogWarning(
                "Registration rejected: email or display name already in use (email={EmailTaken}, name={NameTaken}).",
                emailTaken, nameTaken);
            return AuthResult<Player>.Fail("Email or display name is invalid.");
        }

        // Hash the password. Pass a stub Player — PasswordHasher<T> PBKDF2 implementation
        // does not use the user object; the stub prevents a null-reference in any future override.
        var passwordHash = _passwordHasher.HashPassword(new Player(), password);

        // Generate a 32-byte URL-safe verification token.
        var verificationToken = TokenGenerator.Generate();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            EmailAddress = normalizedEmail,
            DisplayName = displayName,
            PasswordHash = passwordHash,
            EmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiresAt = now.Add(VerificationTokenExpiry),
            CreatedAt = now,
            FailedLoginAttempts = 0
        };

        await _playerRepository.CreateAsync(player, ct);

        _logger.LogInformation(
            "Registered new player {PlayerId} with email {Email}.",
            player.PlayerId, normalizedEmail);

        // Construct the absolute verification URL and send the email.
        var verifyUrl = $"{verifyUrlBase.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(verificationToken)}";
        await _emailSender.SendVerificationAsync(normalizedEmail, displayName, verifyUrl, ct);

        return AuthResult<Player>.Ok(player);
    }

    /// <inheritdoc />
    public async Task<AuthResult<Player>> LoginAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? "";

        var player = await _playerRepository.GetByEmailAsync(normalizedEmail, ct);

        // Timing guard: even when the player is not found, execute a dummy hash verification
        // so the response time is indistinguishable from a real failed verification.
        if (player is null)
        {
            // Discard result — purely for constant-time behavior.
            _passwordHasher.VerifyHashedPassword(new Player(), _dummyHash, password);
            _logger.LogWarning("Login failed: email not found.");
            return AuthResult<Player>.Fail("Invalid credentials.");
        }

        // Check email verification before lockout to give actionable feedback to legitimate users.
        // Per spec §7.5 note: this is an acceptable minor enumeration leak for UX reasons.
        if (!player.EmailVerified)
        {
            _logger.LogWarning("Login failed: email not verified for player {PlayerId}.", player.PlayerId);
            return AuthResult<Player>.Fail("Please verify your email before signing in.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Check account lockout.
        if (player.LockedOutUntil.HasValue && player.LockedOutUntil > now)
        {
            _logger.LogWarning(
                "Login failed: account locked for player {PlayerId} until {LockedOutUntil}.",
                player.PlayerId, player.LockedOutUntil);
            return AuthResult<Player>.Fail("Account locked. Please try again later.");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(player, player.PasswordHash, password);

        if (verifyResult == PasswordVerificationResult.Failed)
        {
            player.FailedLoginAttempts++;

            if (player.FailedLoginAttempts >= MaxFailedAttempts)
            {
                player.LockedOutUntil = now.Add(LockoutDuration);
                player.FailedLoginAttempts = 0; // Reset so next 5-strike window starts fresh after unlock.
                _logger.LogWarning(
                    "Player {PlayerId} locked out until {LockedOutUntil} after {MaxFailedAttempts} failed attempts.",
                    player.PlayerId, player.LockedOutUntil, MaxFailedAttempts);
            }
            else
            {
                _logger.LogWarning(
                    "Login failed for player {PlayerId}: attempt {FailedAttempts}/{MaxFailedAttempts}.",
                    player.PlayerId, player.FailedLoginAttempts, MaxFailedAttempts);
            }

            await _playerRepository.UpdateAsync(player, ct);
            return AuthResult<Player>.Fail("Invalid credentials.");
        }

        // SuccessRehashNeeded: re-hash with the current algorithm before falling through.
        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            player.PasswordHash = _passwordHasher.HashPassword(player, password);
            _logger.LogInformation("Re-hashed password for player {PlayerId}.", player.PlayerId);
        }

        // Successful login: reset failure tracking and record last login time.
        player.FailedLoginAttempts = 0;
        player.LastLoginAt = now;
        player.LockedOutUntil = null;

        await _playerRepository.UpdateAsync(player, ct);

        _logger.LogInformation("Player {PlayerId} logged in successfully.", player.PlayerId);
        return AuthResult<Player>.Ok(player);
    }

    /// <inheritdoc />
    public async Task<AuthResult> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return AuthResult.Fail("Invalid or expired verification link.");

        var player = await _playerRepository.GetByEmailVerificationTokenAsync(token, ct);
        if (player is null)
        {
            _logger.LogWarning("Email verification failed: token not found.");
            return AuthResult.Fail("Invalid or expired verification link.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (player.EmailVerificationTokenExpiresAt < now)
        {
            _logger.LogWarning(
                "Email verification failed: token expired for player {PlayerId}.", player.PlayerId);
            return AuthResult.Fail("Invalid or expired verification link.");
        }

        player.EmailVerified = true;
        player.EmailVerificationToken = null;
        player.EmailVerificationTokenExpiresAt = null;

        await _playerRepository.UpdateAsync(player, ct);

        _logger.LogInformation("Email verified for player {PlayerId}.", player.PlayerId);
        return AuthResult.Ok();
    }

    /// <inheritdoc />
    public async Task<AuthResult> ResendVerificationAsync(
        string email,
        string verifyUrlBase,
        CancellationToken ct = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? "";

        // Always return Ok — no enumeration regardless of outcome.
        var player = await _playerRepository.GetByEmailAsync(normalizedEmail, ct);

        if (player is null || player.EmailVerified)
        {
            // Either not found or already verified — silently succeed.
            return AuthResult.Ok();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var newToken = TokenGenerator.Generate();
        player.EmailVerificationToken = newToken;
        player.EmailVerificationTokenExpiresAt = now.Add(VerificationTokenExpiry);

        await _playerRepository.UpdateAsync(player, ct);

        var verifyUrl = $"{verifyUrlBase.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(newToken)}";
        await _emailSender.SendVerificationAsync(normalizedEmail, player.DisplayName, verifyUrl, ct);

        _logger.LogInformation(
            "Verification email resent for player {PlayerId}.", player.PlayerId);

        return AuthResult.Ok();
    }

    /// <inheritdoc />
    public async Task<AuthResult> ForgotPasswordAsync(
        string email,
        string resetUrlBase,
        CancellationToken ct = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? "";

        // Always return Ok — no enumeration regardless of outcome.
        var player = await _playerRepository.GetByEmailAsync(normalizedEmail, ct);

        if (player is null)
            return AuthResult.Ok();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var resetToken = TokenGenerator.Generate();
        player.PasswordResetToken = resetToken;
        player.PasswordResetTokenExpiresAt = now.Add(PasswordResetTokenExpiry);

        await _playerRepository.UpdateAsync(player, ct);

        var resetUrl = $"{resetUrlBase.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken)}";
        await _emailSender.SendPasswordResetAsync(normalizedEmail, player.DisplayName, resetUrl, ct);

        _logger.LogInformation(
            "Password reset email sent for player {PlayerId}.", player.PlayerId);

        return AuthResult.Ok();
    }

    /// <inheritdoc />
    public async Task<AuthResult> ResetPasswordAsync(
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken ct = default)
    {
        // Validate password rules before any DB lookup to fail fast on bad input.
        if (newPassword != confirmPassword)
            return AuthResult.Fail("Passwords do not match.");

        var passwordErrors = PasswordValidator.Validate(newPassword);
        if (passwordErrors.Count > 0)
        {
            var combined = string.Join(" ", passwordErrors);
            return AuthResult.Fail($"Password requirements: {combined}");
        }

        if (string.IsNullOrWhiteSpace(token))
            return AuthResult.Fail("Invalid or expired reset link.");

        var player = await _playerRepository.GetByPasswordResetTokenAsync(token, ct);
        if (player is null)
        {
            _logger.LogWarning("Password reset failed: token not found.");
            return AuthResult.Fail("Invalid or expired reset link.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (player.PasswordResetTokenExpiresAt < now)
        {
            _logger.LogWarning(
                "Password reset failed: token expired for player {PlayerId}.", player.PlayerId);
            return AuthResult.Fail("Invalid or expired reset link.");
        }

        player.PasswordHash = _passwordHasher.HashPassword(player, newPassword);
        player.PasswordResetToken = null;
        player.PasswordResetTokenExpiresAt = null;

        await _playerRepository.UpdateAsync(player, ct);

        _logger.LogInformation("Password reset completed for player {PlayerId}.", player.PlayerId);
        return AuthResult.Ok();
    }
}
