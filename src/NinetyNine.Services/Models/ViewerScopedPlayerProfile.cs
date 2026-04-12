using NinetyNine.Model;

namespace NinetyNine.Services.Models;

/// <summary>
/// A projection of a <see cref="Player"/> with every gated field either
/// populated or nulled out according to the viewer's
/// <see cref="ViewerRelationship"/> to the target. This is the single
/// type the UI consumes when rendering a profile — UI code must never
/// touch <see cref="Audience"/> or <see cref="ProfileVisibility"/>
/// directly.
/// <para>
/// Non-gated fields (<see cref="PlayerId"/>, <see cref="DisplayName"/>,
/// <see cref="CreatedAt"/>, <see cref="IsOwnProfile"/>) are always
/// populated regardless of relationship. Gated fields are <c>null</c>
/// when the viewer's relationship does not qualify.
/// </para>
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 3 S3.5.</para>
/// </summary>
public sealed record ViewerScopedPlayerProfile
{
    /// <summary>The target player's id. Always populated.</summary>
    public required Guid PlayerId { get; init; }

    /// <summary>
    /// The target's public display name. Always populated — display
    /// name is not gated, it is effectively Public by definition.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// UTC account-creation timestamp. Always populated — used for the
    /// "Member since" line and not treated as private.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Convenience flag: true when the viewer is the target player.
    /// Equivalent to <c>Relationship == ViewerRelationship.Self</c>.
    /// </summary>
    public required bool IsOwnProfile { get; init; }

    /// <summary>
    /// The resolved viewer → target relationship. Exposed for diagnostic
    /// / analytics purposes; UI should prefer the nulled-out fields
    /// rather than re-deriving gating from this value.
    /// </summary>
    public required ViewerRelationship Relationship { get; init; }

    /// <summary>
    /// True when the player's account has been retired (soft-deleted).
    /// PII is erased; only DisplayName and game history survive.
    /// </summary>
    public bool IsRetired { get; init; }

    // ── Gated fields. null == not visible to the viewer ─────────────────

    /// <summary>Email address. Gated by <see cref="ProfileVisibility.EmailAudience"/>.</summary>
    public string? EmailAddress { get; init; }

    /// <summary>Phone number. Gated by <see cref="ProfileVisibility.PhoneAudience"/>.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>First name. Gated by <see cref="ProfileVisibility.RealNameAudience"/>.</summary>
    public string? FirstName { get; init; }

    /// <summary>Middle name. Gated by <see cref="ProfileVisibility.RealNameAudience"/>.</summary>
    public string? MiddleName { get; init; }

    /// <summary>Last name. Gated by <see cref="ProfileVisibility.RealNameAudience"/>.</summary>
    public string? LastName { get; init; }

    /// <summary>
    /// Avatar reference. Gated by <see cref="ProfileVisibility.AvatarAudience"/>,
    /// which defaults to <see cref="Audience.Public"/> — avatars are the
    /// one documented exception to the most-private-default rule.
    /// </summary>
    public AvatarRef? Avatar { get; init; }

    /// <summary>
    /// Convenience: true when any component of the real name (first /
    /// middle / last) is populated. Useful for suppressing the real-name
    /// line entirely when the viewer does not qualify.
    /// </summary>
    public bool HasRealName =>
        !string.IsNullOrWhiteSpace(FirstName)
        || !string.IsNullOrWhiteSpace(MiddleName)
        || !string.IsNullOrWhiteSpace(LastName);
}
