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

    /// <summary>
    /// Player's <a href="https://fargorate.com/">FargoRate</a> skill rating
    /// (typical range 200–850). Null when unrated. Stored verbatim — the
    /// app does not compute or update Fargo (FargoRate is the system of
    /// record); the value is captured so the UI can display it on the
    /// profile, leaderboards, and match preview screens, and so the dev
    /// data seeder can generate game histories whose scores realistically
    /// match the player's claimed skill bracket.
    /// <para>
    /// Why Fargo and not APA SL: Fargo is absolute and venue-portable —
    /// "550 in Huntsville" means the same thing as "550 in Houston" — and
    /// is the rating cited by serious players sizing each other up. APA SL
    /// is league-internal and handicapped (opaque formula); useful for APA
    /// match handicapping, useless as a portable skill measure. The seed
    /// data comments include a loose APA→Fargo crosswalk for users from
    /// the Huntsville league scene who recognize SL numbers.
    /// </para>
    /// </summary>
    public int? FargoRating { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete / retirement (Sprint 7 S7.2) ───────────────────

    /// <summary>
    /// UTC timestamp when the player's account was retired (soft-deleted).
    /// Null for active players. When set, PII has been erased and the
    /// display name is retired (no other player can claim it). Game
    /// records survive with the original PlayerId.
    /// </summary>
    public DateTime? RetiredAt { get; set; }

    /// <summary>
    /// HMAC-SHA256 hash of the player's email address, computed at
    /// deletion time using an application-level key. Used for O(1)
    /// re-registration prevention. Null for active players.
    /// </summary>
    public string? EmailHash { get; set; }

    /// <summary>
    /// UTC timestamp when deletion will execute (7-day cooldown).
    /// Set by <c>IPlayerService.InitiateDeleteAsync</c>, cleared by
    /// <c>CancelDeleteAsync</c>. The expiration sweep in DataSeeder
    /// calls <c>ExecuteDeleteAsync</c> for players past this date.
    /// Null when no deletion is scheduled.
    /// </summary>
    public DateTime? DeletionScheduledAt { get; set; }

    /// <summary>
    /// Schema evolution marker. Values:
    /// <list type="bullet">
    /// <item><b>0 or missing</b> — legacy pre-Sprint-0 player. Visibility uses
    /// the bool flags only; Audience enum properties are at default. The
    /// heal pass migrates to version 2 on next startup.</item>
    /// <item><b>2</b> — Sprint 0 migrated. Audience enum properties reflect
    /// the intended state; bool flags are still written for backward read
    /// compatibility but will be removed in Sprint 3.</item>
    /// </list>
    /// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.5.</para>
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Transient flag set by the bool → Audience heal pass. When true, the
    /// Edit Profile page shows a one-time banner explaining what changed
    /// and how to widen any field back. The dismiss button flips it to true.
    /// </summary>
    public bool MigrationBannerDismissed { get; set; }
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
/// player's profile. Each field uses the <see cref="Audience"/> enum.
/// <para>
/// Legacy bool flags (EmailAddress, PhoneNumber, RealName, Avatar) were
/// removed in Sprint 6 S6.2. Old Mongo documents that still contain them
/// deserialize cleanly via <c>SetIgnoreExtraElements(true)</c> on the
/// <c>ProfileVisibility</c> BSON class map.
/// </para>
/// </summary>
public class ProfileVisibility
{
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
