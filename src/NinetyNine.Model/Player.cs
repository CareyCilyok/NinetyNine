namespace NinetyNine.Model;

/// <summary>
/// Represents a registered player in the NinetyNine application.
/// </summary>
public class Player
{
    public Guid PlayerId { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// The player's email address. Required and must be unique across all players.
    /// Stored lowercased at write time; the MongoDB unique index enforces uniqueness
    /// in a case-insensitive fashion. Was previously optional — is now required.
    /// </summary>
    public string EmailAddress { get; set; } = "";

    /// <summary>
    /// PBKDF2 password hash produced by <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;Player&gt;</c>.
    /// Empty string until WP-05 wires the hasher. Never transmitted to clients.
    /// </summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>
    /// Indicates whether the player has verified ownership of their email address.
    /// Initially <c>false</c>; set to <c>true</c> when the verification link is clicked.
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// A 32-byte, URL-safe base64 token sent to the player's email for address verification.
    /// Null until registration, and cleared (set to null) once verification completes.
    /// </summary>
    public string? EmailVerificationToken { get; set; }

    /// <summary>
    /// UTC expiry time for <see cref="EmailVerificationToken"/>.
    /// Null when no verification is pending. Typically set to 24 hours after registration.
    /// </summary>
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    /// <summary>
    /// A 32-byte, URL-safe base64 token issued when the player requests a password reset.
    /// Null until a reset is requested, and cleared once the reset completes.
    /// </summary>
    public string? PasswordResetToken { get; set; }

    /// <summary>
    /// UTC expiry time for <see cref="PasswordResetToken"/>.
    /// Null when no reset is pending. Typically set to 1 hour after the reset request.
    /// </summary>
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    /// <summary>
    /// UTC timestamp of the player's most recent successful login.
    /// Null until the player logs in for the first time.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Number of consecutive failed login attempts since the last successful login.
    /// Resets to 0 on successful login. Used together with <see cref="LockedOutUntil"/>
    /// to enforce account lockout after repeated failures.
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// UTC timestamp until which the account is locked out due to repeated failed login attempts.
    /// Null when the account is not locked. Cleared (set to null) on successful login.
    /// </summary>
    public DateTime? LockedOutUntil { get; set; }

    // Optional PII — visibility controlled per field
    public string? PhoneNumber { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }

    public ProfileVisibility Visibility { get; set; } = new();
    public AvatarRef? Avatar { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-field visibility flags controlling what other users can see on a player's profile.
/// </summary>
public class ProfileVisibility
{
    /// <summary>Whether the email address is publicly visible. Defaults to hidden.</summary>
    public bool EmailAddress { get; set; } = false;

    /// <summary>Whether the phone number is publicly visible. Defaults to hidden.</summary>
    public bool PhoneNumber { get; set; } = false;

    /// <summary>Controls First/Middle/Last name together. Defaults to hidden.</summary>
    public bool RealName { get; set; } = false;

    /// <summary>Whether the avatar is publicly visible. Defaults to visible.</summary>
    public bool Avatar { get; set; } = true;
}

/// <summary>
/// Metadata for an uploaded avatar image stored in GridFS.
/// </summary>
public class AvatarRef
{
    /// <summary>GridFS ObjectId serialized as a string.</summary>
    public string StorageKey { get; set; } = "";

    public string ContentType { get; set; } = "";
    public int WidthPx { get; set; }
    public int HeightPx { get; set; }
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
