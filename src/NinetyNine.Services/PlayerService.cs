using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Repository.Storage;
using NinetyNine.Services.Models;

namespace NinetyNine.Services;

/// <summary>
/// Implements player registration, login, profile management, and avatar operations.
/// </summary>
public sealed partial class PlayerService(
    IPlayerRepository playerRepository,
    IAvatarStore avatarStore,
    AvatarService avatarService,
    ILogger<PlayerService> logger)
    : IPlayerService
{
    [GeneratedRegex(@"^[a-zA-Z0-9_\-]{2,32}$", RegexOptions.Compiled)]
    private static partial Regex DisplayNameRegex();

    public async Task<Player> RegisterAsync(
        string displayName, string provider, string providerUserId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        // provider and providerUserId are kept in the signature for interface compatibility;
        // they are no longer persisted on the Player. WP-05 will replace this method body
        // with email/password registration.

        ValidateDisplayNameFormat(displayName);

        bool nameExists = await playerRepository.DisplayNameExistsAsync(displayName, ct);
        if (nameExists)
            throw new InvalidOperationException($"Display name '{displayName}' is already taken.");

        var player = new Player
        {
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };

        await playerRepository.CreateAsync(player, ct);
        logger.LogInformation(
            "Registered new player {PlayerId} with DisplayName '{DisplayName}'",
            player.PlayerId, displayName);

        return player;
    }

    public Task<Player?> LoginAsync(
        string provider, string providerUserId, CancellationToken ct = default)
    {
        // TODO(WP-05): implement email/password login. OAuth login removed in WP-01.
        return Task.FromResult<Player?>(null);
    }

    public async Task<Player> UpdateProfileAsync(
        Guid playerId, PlayerProfileUpdate update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var player = await GetRequiredPlayerAsync(playerId, ct);

        if (update.DisplayName is not null)
        {
            ValidateDisplayNameFormat(update.DisplayName);

            if (!string.Equals(update.DisplayName, player.DisplayName, StringComparison.Ordinal))
            {
                bool nameExists = await playerRepository.DisplayNameExistsAsync(update.DisplayName, ct);
                if (nameExists)
                    throw new InvalidOperationException($"Display name '{update.DisplayName}' is already taken.");

                player.DisplayName = update.DisplayName;
            }
        }

        if (update.EmailAddress is not null) player.EmailAddress = update.EmailAddress.ToLowerInvariant();
        if (update.PhoneNumber is not null) player.PhoneNumber = update.PhoneNumber;
        if (update.FirstName is not null) player.FirstName = update.FirstName;
        if (update.MiddleName is not null) player.MiddleName = update.MiddleName;
        if (update.LastName is not null) player.LastName = update.LastName;
        if (update.Visibility is not null) player.Visibility = update.Visibility;

        await playerRepository.UpdateAsync(player, ct);
        logger.LogDebug("Updated profile for player {PlayerId}", playerId);

        return player;
    }

    public async Task<bool> IsDisplayNameAvailableAsync(
        string displayName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return false;

        if (!DisplayNameRegex().IsMatch(displayName))
            return false;

        return !await playerRepository.DisplayNameExistsAsync(displayName, ct);
    }

    public async Task SetAvatarAsync(
        Guid playerId, Stream imageContent, string contentType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var player = await GetRequiredPlayerAsync(playerId, ct);

        // Remove old avatar if present
        if (player.Avatar is not null)
        {
            logger.LogDebug("Removing old avatar {StorageKey} for player {PlayerId}",
                player.Avatar.StorageKey, playerId);
            await avatarStore.DeleteAsync(player.Avatar.StorageKey, ct);
        }

        player.Avatar = await avatarService.ProcessAndStoreAsync(player, imageContent, contentType, ct);
        await playerRepository.UpdateAsync(player, ct);
    }

    public async Task RemoveAvatarAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await GetRequiredPlayerAsync(playerId, ct);

        if (player.Avatar is null)
        {
            logger.LogDebug("Player {PlayerId} has no avatar to remove", playerId);
            return;
        }

        await avatarStore.DeleteAsync(player.Avatar.StorageKey, ct);
        player.Avatar = null;
        await playerRepository.UpdateAsync(player, ct);
        logger.LogInformation("Removed avatar for player {PlayerId}", playerId);
    }

    private async Task<Player> GetRequiredPlayerAsync(Guid playerId, CancellationToken ct)
    {
        var player = await playerRepository.GetByIdAsync(playerId, ct);
        if (player is null)
            throw new KeyNotFoundException($"Player {playerId} not found.");
        return player;
    }

    private static void ValidateDisplayNameFormat(string displayName)
    {
        if (!DisplayNameRegex().IsMatch(displayName))
            throw new ArgumentException(
                "Display name must be 2–32 characters and contain only letters, digits, underscores, or hyphens.",
                nameof(displayName));
    }
}
