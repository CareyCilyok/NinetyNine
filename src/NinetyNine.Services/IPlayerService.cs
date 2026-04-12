using NinetyNine.Model;
using NinetyNine.Services.Models;

namespace NinetyNine.Services;

/// <summary>
/// Manages player registration, login, profile updates, and avatar management.
/// </summary>
public interface IPlayerService
{
    /// <summary>
    /// Registers a new player with a unique display name, linked to an external OAuth identity.
    /// </summary>
    Task<Player> RegisterAsync(string displayName, string provider, string providerUserId, CancellationToken ct = default);

    /// <summary>
    /// Looks up an existing player by their external identity. Returns null if not registered.
    /// </summary>
    Task<Player?> LoginAsync(string provider, string providerUserId, CancellationToken ct = default);

    /// <summary>
    /// Applies a partial update to a player's profile. Null fields in <paramref name="update"/> are ignored.
    /// </summary>
    Task<Player> UpdateProfileAsync(Guid playerId, PlayerProfileUpdate update, CancellationToken ct = default);

    /// <summary>
    /// Returns true when the display name is not already taken and passes validation rules.
    /// </summary>
    Task<bool> IsDisplayNameAvailableAsync(string displayName, CancellationToken ct = default);

    /// <summary>
    /// Processes, resizes, and stores an avatar image for the player.
    /// Delegates to <see cref="AvatarService"/> for image processing.
    /// </summary>
    Task SetAvatarAsync(Guid playerId, Stream imageContent, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Removes the player's current avatar from storage and clears the reference.
    /// </summary>
    Task RemoveAvatarAsync(Guid playerId, CancellationToken ct = default);

    /// <summary>
    /// Returns a viewer-scoped projection of the target player's profile,
    /// applying the Friends + Communities audience matrix (plan S3.5).
    /// The returned <see cref="ViewerScopedPlayerProfile"/> has every
    /// gated field either populated or <c>null</c> based on the viewer's
    /// resolved <see cref="ViewerRelationship"/> to the target. Callers
    /// must never apply gating themselves — this is the single read
    /// path the UI uses for profile rendering.
    /// <para>
    /// Returns <c>null</c> when the target player does not exist.
    /// </para>
    /// <para>
    /// Pass <c>viewerId = null</c> for an unauthenticated viewer. The
    /// anonymous relationship reveals only the display name, creation
    /// timestamp, and — when <see cref="ProfileVisibility.AvatarAudience"/>
    /// is <see cref="Audience.Public"/> — the avatar.
    /// </para>
    /// </summary>
    /// <param name="targetId">The player whose profile is being rendered.</param>
    /// <param name="viewerId">The viewing player, or <c>null</c> for anonymous.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ViewerScopedPlayerProfile?> GetProfileForViewerAsync(
        Guid targetId,
        Guid? viewerId,
        CancellationToken ct = default);
}
