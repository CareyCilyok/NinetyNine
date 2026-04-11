using NinetyNine.Model;

namespace NinetyNine.Services.Auth;

/// <summary>
/// Domain service for email/password-based authentication operations.
/// All methods are designed to resist user enumeration: register, forgot-password,
/// and resend-verification return identical responses whether or not an account exists.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new player with the given email address, display name, and password.
    /// Sends a verification email on success.
    /// </summary>
    /// <param name="email">Raw email address (will be normalized to lowercase).</param>
    /// <param name="displayName">Chosen display name (2–32 chars, <c>[a-zA-Z0-9_-]</c>).</param>
    /// <param name="password">Plain-text password (must pass all complexity rules).</param>
    /// <param name="confirmPassword">Must match <paramref name="password"/> exactly.</param>
    /// <param name="verifyUrlBase">Absolute base URL (e.g. <c>https://localhost:8080</c>) used to construct the verification link.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="AuthResult{Player}.Ok"/> with the new player on success;
    /// <see cref="AuthResult{Player}.Fail"/> with a human-readable error message on failure.
    /// On failure, the error is intentionally generic to prevent user enumeration.
    /// </returns>
    Task<AuthResult<Player>> RegisterAsync(
        string email,
        string displayName,
        string password,
        string confirmPassword,
        string verifyUrlBase,
        CancellationToken ct = default);

    /// <summary>
    /// Authenticates a player using their email address and password.
    /// Enforces account lockout after 5 consecutive failures (15-minute window).
    /// Always performs a hash computation even when the account is not found,
    /// making the timing uniform regardless of whether the email exists.
    /// </summary>
    /// <param name="email">Raw email address.</param>
    /// <param name="password">Plain-text password to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="AuthResult{Player}.Ok"/> with the authenticated player on success;
    /// <see cref="AuthResult{Player}.Fail"/> with <c>"Invalid credentials."</c> on any failure.
    /// </returns>
    Task<AuthResult<Player>> LoginAsync(
        string email,
        string password,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies the email address associated with the given one-time token.
    /// Sets <c>EmailVerified = true</c> and clears the token on success.
    /// </summary>
    /// <param name="token">URL-safe base64 token from the verification email.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="AuthResult.Ok"/> on success;
    /// <see cref="AuthResult.Fail"/> with <c>"Invalid or expired verification link."</c> if the
    /// token is unknown or has passed its expiry time.
    /// </returns>
    Task<AuthResult> VerifyEmailAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Re-sends the email verification link for an unverified account.
    /// Always returns <see cref="AuthResult.Ok"/> regardless of whether the email exists,
    /// to prevent user enumeration.
    /// </summary>
    /// <param name="email">Raw email address of the account requesting re-verification.</param>
    /// <param name="verifyUrlBase">Absolute base URL used to construct the verification link.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuthResult> ResendVerificationAsync(
        string email,
        string verifyUrlBase,
        CancellationToken ct = default);

    /// <summary>
    /// Initiates the forgot-password flow. Generates a password reset token and sends
    /// an email if the address is registered. Always returns <see cref="AuthResult.Ok"/>
    /// regardless of whether the email exists, to prevent user enumeration.
    /// </summary>
    /// <param name="email">Raw email address of the account requesting a reset.</param>
    /// <param name="resetUrlBase">Absolute base URL used to construct the reset link.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuthResult> ForgotPasswordAsync(
        string email,
        string resetUrlBase,
        CancellationToken ct = default);

    /// <summary>
    /// Completes the password reset flow by validating the token and updating the password hash.
    /// Clears the reset token after a successful update.
    /// </summary>
    /// <param name="token">URL-safe base64 token from the password reset email.</param>
    /// <param name="newPassword">New plain-text password (must pass all complexity rules).</param>
    /// <param name="confirmPassword">Must match <paramref name="newPassword"/> exactly.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="AuthResult.Ok"/> on success;
    /// <see cref="AuthResult.Fail"/> with a human-readable error on validation failure or
    /// if the token is invalid or expired.
    /// </returns>
    Task<AuthResult> ResetPasswordAsync(
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken ct = default);
}
