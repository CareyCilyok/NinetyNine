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
/// Four-tier visibility audience for a profile field. Ordered most-private
/// first so relationship checks can use simple integer comparison:
/// <c>relationship &gt;= fieldAudience</c>.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> for the locked semantics.</para>
/// </summary>
public enum Audience
{
    /// <summary>
    /// Self only. No other user — friend, community member, admin — can see
    /// the field. (There is no instance admin role in v1.0 anyway.)
    /// </summary>
    Private = 0,

    /// <summary>
    /// Self plus mutual, accepted friends. One-sided requests grant nothing.
    /// Unfriending revokes immediately.
    /// </summary>
    Friends = 1,

    /// <summary>
    /// Self plus any user who shares at least one community with the viewer
    /// where both are current, approved members. Public and private
    /// community status does not change this rule for the viewer — both
    /// count as "shared membership". Union applies when multiple
    /// communities are shared (most-permissive wins within the tier).
    /// </summary>
    Communities = 2,

    /// <summary>
    /// Self plus any authenticated NinetyNine user. <b>Does NOT</b> mean the
    /// open internet — unauthenticated visitors never see profile data.
    /// If "internet public / search-indexable" is ever wanted, add a new
    /// <c>World</c> tier; do not conflate.
    /// </summary>
    Public = 3,
}

/// <summary>
/// Per-field visibility settings controlling what other users can see on a
/// player's profile. Schema version 2 uses the <see cref="Audience"/> enum
/// per field; schema version 1 (legacy) used bool flags. The bool
/// properties are kept temporarily with <see cref="ObsoleteAttribute"/>
/// and are migrated on startup by <c>DataSeeder.HealProfileVisibilityAsync</c>.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0.</para>
/// </summary>
public class ProfileVisibility
{
    // ── Audience enum per field (schema version 2) ──────────────────────

    /// <summary>Who can see the email address. Defaults to <see cref="Audience.Private"/>.</summary>
    public Audience EmailAudience { get; set; } = Audience.Private;

    /// <summary>Who can see the phone number. Defaults to <see cref="Audience.Private"/>.</summary>
    public Audience PhoneAudience { get; set; } = Audience.Private;

    /// <summary>Who can see the First/Middle/Last name. Defaults to <see cref="Audience.Private"/>.</summary>
    public Audience RealNameAudience { get; set; } = Audience.Private;

    /// <summary>
    /// Who can see the avatar. Defaults to <see cref="Audience.Public"/> —
    /// the one documented exception to the "most-private default" rule,
    /// preserving the existing behavior that avatars appear in leaderboards,
    /// game history, and badges without explicit opt-in.
    /// </summary>
    public Audience AvatarAudience { get; set; } = Audience.Public;

    // ── Legacy bool flags (schema version 1) ────────────────────────────
    // These remain for one sprint so that existing reads in Profile.razor,
    // EditProfile.razor, Login.razor, and DataSeeder continue to compile
    // while the heal pass (Sprint 0 S0.5) migrates stored values into the
    // *Audience properties above. They will be removed in Sprint 3 once
    // GetProfileForViewerAsync is the single read path. The project builds
    // under TreatWarningsAsErrors, so these are NOT marked [Obsolete] —
    // obsolete usage would cascade into ~20 build errors at call sites.

    /// <summary>Legacy schema-v1 bool flag. Use <see cref="EmailAudience"/>.</summary>
    public bool EmailAddress { get; set; } = false;

    /// <summary>Legacy schema-v1 bool flag. Use <see cref="PhoneAudience"/>.</summary>
    public bool PhoneNumber { get; set; } = false;

    /// <summary>Legacy schema-v1 bool flag. Use <see cref="RealNameAudience"/>.</summary>
    public bool RealName { get; set; } = false;

    /// <summary>Legacy schema-v1 bool flag. Use <see cref="AvatarAudience"/>.</summary>
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
