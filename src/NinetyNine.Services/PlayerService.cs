using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
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
    IFriendshipRepository friendshipRepository,
    ICommunityMemberRepository communityMemberRepository,
    ICommunityRepository communityRepository,
    IPlayerBlockRepository playerBlockRepository,
    IConfiguration configuration,
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

    /// <inheritdoc/>
    public async Task<ViewerScopedPlayerProfile?> GetProfileForViewerAsync(
        Guid targetId, Guid? viewerId, CancellationToken ct = default)
    {
        var target = await playerRepository.GetByIdAsync(targetId, ct);
        if (target is null) return null;

        var relationship = await ResolveRelationshipAsync(targetId, viewerId, ct);
        return Project(target, relationship);
    }

    /// <summary>
    /// Resolves the viewer → target relationship in the locked order:
    /// Self → Friend → CommunityMember → Public → Anonymous. Performs at
    /// most two data-store round trips for non-trivial cases (friendship
    /// lookup + shared-community intersection) and returns immediately
    /// on the first match.
    /// </summary>
    private async Task<ViewerRelationship> ResolveRelationshipAsync(
        Guid targetId, Guid? viewerId, CancellationToken ct)
    {
        if (viewerId is null) return ViewerRelationship.Anonymous;
        var vid = viewerId.Value;

        if (vid == targetId) return ViewerRelationship.Self;

        var friendship = await friendshipRepository.GetByPairAsync(vid, targetId, ct);
        if (friendship is not null) return ViewerRelationship.Friend;

        if (await SharesCommunityAsync(vid, targetId, ct))
            return ViewerRelationship.CommunityMember;

        return ViewerRelationship.Public;
    }

    /// <summary>
    /// Returns true when the viewer and target are co-members of at
    /// least one community. Uses two single-filter lookups against the
    /// <c>ux_community_members_player_community</c> index (one per
    /// player) and intersects in memory — the working set is tiny
    /// (≤ soft cap of 10 communities per player) so a single aggregate
    /// pipeline would be more machinery than the call deserves.
    /// </summary>
    private async Task<bool> SharesCommunityAsync(
        Guid viewerId, Guid targetId, CancellationToken ct)
    {
        var viewerMemberships = await communityMemberRepository
            .ListCommunitiesForPlayerAsync(viewerId, ct);
        if (viewerMemberships.Count == 0) return false;

        var targetMemberships = await communityMemberRepository
            .ListCommunitiesForPlayerAsync(targetId, ct);
        if (targetMemberships.Count == 0) return false;

        var viewerIds = new HashSet<Guid>(viewerMemberships.Select(m => m.CommunityId));
        return targetMemberships.Any(m => viewerIds.Contains(m.CommunityId));
    }

    /// <summary>
    /// Applies the audience matrix to a target player and produces the
    /// viewer-scoped projection. The comparison uses
    /// <c>(int)relationship &lt;= (int)audience</c>, which matches the
    /// enum ordering (most-private-first) and the semantic "my floor
    /// is at or below the field's audience tier".
    /// </summary>
    private static ViewerScopedPlayerProfile Project(
        Player target, ViewerRelationship relationship)
    {
        bool Visible(Audience audience) => CanSee(relationship, audience);

        var vis = target.Visibility;
        bool realNameVisible = Visible(vis.RealNameAudience);

        return new ViewerScopedPlayerProfile
        {
            PlayerId = target.PlayerId,
            DisplayName = target.DisplayName,
            CreatedAt = target.CreatedAt,
            IsOwnProfile = relationship == ViewerRelationship.Self,
            Relationship = relationship,
            IsRetired = target.RetiredAt is not null,
            EmailAddress = Visible(vis.EmailAudience) ? NullIfBlank(target.EmailAddress) : null,
            PhoneNumber = Visible(vis.PhoneAudience) ? NullIfBlank(target.PhoneNumber) : null,
            FirstName = realNameVisible ? NullIfBlank(target.FirstName) : null,
            MiddleName = realNameVisible ? NullIfBlank(target.MiddleName) : null,
            LastName = realNameVisible ? NullIfBlank(target.LastName) : null,
            Avatar = Visible(vis.AvatarAudience) ? target.Avatar : null,
        };
    }

    /// <summary>
    /// Audience gate. Anonymous viewers only ever see Public fields.
    /// Authenticated viewers use the integer comparison described in
    /// <see cref="ViewerRelationship"/>.
    /// </summary>
    private static bool CanSee(ViewerRelationship relationship, Audience audience)
    {
        if (relationship == ViewerRelationship.Anonymous)
            return audience == Audience.Public;

        return (int)relationship <= (int)audience;
    }

    private static string? NullIfBlank(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    // ── Account deletion (Sprint 7 S7.3) ───────────────────────────

    private static readonly TimeSpan DeletionCooldown = TimeSpan.FromDays(7);

    public async Task<ServiceResult> InitiateDeleteAsync(
        Guid playerId, CancellationToken ct = default)
    {
        var player = await playerRepository.GetByIdAsync(playerId, ct);
        if (player is null) return ServiceResult.Fail("PlayerNotFound", "Player not found.");

        if (player.RetiredAt is not null)
            return ServiceResult.Fail("AlreadyRetired", "This account is already deleted.");

        if (player.DeletionScheduledAt is not null)
            return ServiceResult.Fail("DeletionAlreadyScheduled", "Deletion is already scheduled.");

        // Block if player owns communities.
        var owned = await ListOwnedCommunitiesBlockingDeletionAsync(playerId, ct);
        if (owned.Count > 0)
            return ServiceResult.Fail("OwnerMustTransferFirst",
                $"Transfer ownership of {owned.Count} community(ies) before deleting your account.");

        player.DeletionScheduledAt = DateTime.UtcNow + DeletionCooldown;
        await playerRepository.UpdateAsync(player, ct);

        logger.LogInformation(
            "Deletion scheduled for player {PlayerId} at {ScheduledAt}",
            playerId, player.DeletionScheduledAt);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelDeleteAsync(
        Guid playerId, CancellationToken ct = default)
    {
        var player = await playerRepository.GetByIdAsync(playerId, ct);
        if (player is null) return ServiceResult.Fail("PlayerNotFound", "Player not found.");

        if (player.DeletionScheduledAt is null)
            return ServiceResult.Fail("NoDeletionScheduled", "No deletion is scheduled.");

        player.DeletionScheduledAt = null;
        await playerRepository.UpdateAsync(player, ct);

        logger.LogInformation("Deletion cancelled for player {PlayerId}", playerId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ExecuteDeleteAsync(
        Guid playerId, CancellationToken ct = default)
    {
        var player = await playerRepository.GetByIdAsync(playerId, ct);
        if (player is null) return ServiceResult.Fail("PlayerNotFound", "Player not found.");

        if (player.RetiredAt is not null)
            return ServiceResult.Ok(); // Already retired — idempotent.

        // Compute email hash before clearing PII.
        var hashKey = configuration["Security:EmailHashKey"] ?? "default-key";
        player.EmailHash = ComputeEmailHash(player.EmailAddress, hashKey);

        // Erase PII.
        player.EmailAddress = "";
        player.PhoneNumber = null;
        player.FirstName = null;
        player.MiddleName = null;
        player.LastName = null;
        player.PasswordHash = "";
        player.EmailVerified = false;
        player.EmailVerificationToken = null;
        player.EmailVerificationTokenExpiresAt = null;
        player.PasswordResetToken = null;
        player.PasswordResetTokenExpiresAt = null;
        player.FailedLoginAttempts = 0;
        player.LockedOutUntil = null;
        player.LastLoginAt = null;
        player.MigrationBannerDismissed = true;
        player.Visibility = new ProfileVisibility(); // All Private defaults.
        player.DeletionScheduledAt = null;
        player.RetiredAt = DateTime.UtcNow;

        // Delete avatar from GridFS.
        if (player.Avatar is not null)
        {
            await avatarStore.DeleteAsync(player.Avatar.StorageKey, ct);
            player.Avatar = null;
        }

        await playerRepository.UpdateAsync(player, ct);

        // Cascade: remove friendships.
        var friendships = await friendshipRepository.ListForPlayerAsync(playerId, ct);
        foreach (var f in friendships)
            await friendshipRepository.DeleteAsync(f.PlayerAId, f.PlayerBId, ct);

        // Cascade: remove community memberships.
        var memberships = await communityMemberRepository.ListCommunitiesForPlayerAsync(playerId, ct);
        foreach (var m in memberships)
            await communityMemberRepository.RemoveAsync(m.CommunityId, playerId, ct);

        // Cascade: remove blocks.
        var blockedIds = await playerBlockRepository.ListBlockedIdsAsync(playerId, ct);
        foreach (var blockedId in blockedIds)
        {
            var block = await playerBlockRepository.GetBlockAsync(playerId, blockedId, ct);
            if (block is not null)
                await playerBlockRepository.DeleteAsync(block.BlockId, ct);
        }

        logger.LogInformation(
            "Account deleted for player {PlayerId} ({DisplayName}). " +
            "{Friendships} friendships, {Memberships} memberships, {Blocks} blocks removed.",
            playerId, player.DisplayName,
            friendships.Count, memberships.Count, blockedIds.Count);

        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<Community>> ListOwnedCommunitiesBlockingDeletionAsync(
        Guid playerId, CancellationToken ct = default)
    {
        var owned = await communityRepository.ListByOwnerPlayerAsync(playerId, ct);
        return owned;
    }

    private static string ComputeEmailHash(string email, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var emailBytes = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
        var hash = HMACSHA256.HashData(keyBytes, emailBytes);
        return Convert.ToBase64String(hash);
    }
}
