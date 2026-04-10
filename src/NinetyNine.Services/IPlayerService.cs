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
}
