using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

/// <summary>
/// Default <see cref="ICommunityService"/> implementation. Enforces
/// every invariant, rate limit, and authorization rule from the plan's
/// Sprint 2 S2.1 acceptance criteria. Talks to the four community
/// repositories plus <see cref="IPlayerRepository"/> for display-name
/// lookups and <see cref="IVenueRepository"/> for the cascade null-out
/// on delete.
/// </summary>
public sealed class CommunityService(
    ICommunityRepository communities,
    ICommunityMemberRepository members,
    ICommunityInvitationRepository invitations,
    ICommunityJoinRequestRepository joinRequests,
    IPlayerRepository players,
    IVenueRepository venues,
    IOwnershipTransferRepository transfers,
    INotificationService notificationService,
    ILogger<CommunityService> logger) : ICommunityService
{
    private static readonly TimeSpan TransferExpiryWindow = TimeSpan.FromDays(7);
    // Locked in the plan's Sprint 2 S2.1 acceptance criteria.
    private const int MaxCommunitiesPerOwner = 10;
    private const int MaxInvitesPerInviterPerTargetPerYear = 5;
    private static readonly TimeSpan InviteRateLimitWindow = TimeSpan.FromDays(365);
    private const int NameMinLength = 2;
    private const int NameMaxLength = 40;

    // ── Create / update / delete / transfer ─────────────────────────

    public async Task<ServiceResult<Community>> CreatePlayerOwnedAsync(
        Guid ownerPlayerId,
        string name,
        string slug,
        string? description,
        CommunityVisibility visibility,
        CancellationToken ct = default)
    {
        var normalizedName = (name ?? string.Empty).Trim();
        var normalizedSlug = (slug ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedName.Length is < NameMinLength or > NameMaxLength
            || string.IsNullOrEmpty(normalizedSlug))
        {
            return ServiceResult<Community>.Fail(
                "InvalidCommunityInput",
                $"Community name must be {NameMinLength}–{NameMaxLength} characters and slug cannot be empty.");
        }

        var owner = await players.GetByIdAsync(ownerPlayerId, ct);
        if (owner is null)
            return ServiceResult<Community>.Fail(
                "OwnerNotFound", "The owning player does not exist.");

        var owned = await communities.ListByOwnerPlayerAsync(ownerPlayerId, ct);
        if (owned.Count >= MaxCommunitiesPerOwner)
            return ServiceResult<Community>.Fail(
                "CommunityCapExceeded",
                $"You can own at most {MaxCommunitiesPerOwner} communities.");

        var nameCollision = await communities.GetByNameAsync(normalizedName, ct);
        if (nameCollision is not null)
            return ServiceResult<Community>.Fail(
                "CommunityNameTaken", "A community with that name already exists.");

        var slugCollision = await communities.GetBySlugAsync(normalizedSlug, ct);
        if (slugCollision is not null)
            return ServiceResult<Community>.Fail(
                "CommunitySlugTaken", "A community with that URL slug already exists.");

        var community = new Community
        {
            Name = normalizedName,
            Slug = normalizedSlug,
            Description = TrimDescription(description),
            Visibility = visibility,
            OwnerPlayerId = ownerPlayerId,
            CreatedByPlayerId = ownerPlayerId,
            CreatedAt = DateTime.UtcNow,
            SchemaVersion = 2,
        };

        try
        {
            await communities.CreateAsync(community, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            return ServiceResult<Community>.Fail(
                "CommunityNameTaken",
                "A community with that name or slug already exists.");
        }

        // Owner gets an Owner-role membership row immediately.
        await members.AddAsync(new CommunityMembership
        {
            CommunityId = community.CommunityId,
            PlayerId = ownerPlayerId,
            Role = CommunityRole.Owner,
            JoinedAt = DateTime.UtcNow,
        }, ct);

        logger.LogInformation(
            "Created community {CommunityId} '{Name}' owned by player {OwnerId}",
            community.CommunityId, community.Name, ownerPlayerId);

        return ServiceResult<Community>.Ok(community);
    }

    public async Task<ServiceResult<Community>> UpdateAsync(
        Guid communityId,
        Guid byPlayerId,
        CommunityUpdate changes,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult<Community>.Fail("CommunityNotFound", "Community not found.");

        if (!IsOwner(community, byPlayerId))
            return ServiceResult<Community>.Fail(
                "NotAuthorized", "Only the community owner can edit settings.");

        if (changes.Name is not null)
        {
            var name = changes.Name.Trim();
            if (name.Length is < NameMinLength or > NameMaxLength)
                return ServiceResult<Community>.Fail(
                    "InvalidCommunityInput",
                    $"Community name must be {NameMinLength}–{NameMaxLength} characters.");

            if (!name.Equals(community.Name, StringComparison.OrdinalIgnoreCase))
            {
                var collision = await communities.GetByNameAsync(name, ct);
                if (collision is not null && collision.CommunityId != communityId)
                    return ServiceResult<Community>.Fail(
                        "CommunityNameTaken", "A community with that name already exists.");
            }
            community.Name = name;
        }

        if (changes.Slug is not null)
        {
            var slug = changes.Slug.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(slug))
                return ServiceResult<Community>.Fail(
                    "InvalidCommunityInput", "Slug cannot be empty.");

            if (slug != community.Slug)
            {
                var collision = await communities.GetBySlugAsync(slug, ct);
                if (collision is not null && collision.CommunityId != communityId)
                    return ServiceResult<Community>.Fail(
                        "CommunitySlugTaken", "That URL slug is already taken.");
            }
            community.Slug = slug;
        }

        if (changes.Description is not null)
            community.Description = TrimDescription(changes.Description);

        if (changes.Visibility is not null)
            community.Visibility = changes.Visibility.Value;

        try
        {
            await communities.UpdateAsync(community, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            return ServiceResult<Community>.Fail(
                "CommunityNameTaken",
                "A community with that name or slug already exists.");
        }

        return ServiceResult<Community>.Ok(community);
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid communityId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult.Fail("CommunityNotFound", "Community not found.");

        if (!IsOwner(community, byPlayerId))
            return ServiceResult.Fail(
                "NotAuthorized", "Only the community owner can delete the community.");

        // Cascade: bulk-delete every membership row, bulk-clear every
        // venue affiliation, then delete the community doc itself.
        // Invitations and join requests are left in place — their rows
        // still describe history, and no new activity is possible once
        // the community is gone.
        await members.RemoveAllFromCommunityAsync(communityId, ct);
        await venues.ClearCommunityAffiliationsAsync(communityId, ct);
        await communities.DeleteAsync(communityId, ct);

        logger.LogInformation("Deleted community {CommunityId}", communityId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<OwnershipTransfer>> TransferOwnershipAsync(
        Guid communityId,
        Guid newOwnerPlayerId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult<OwnershipTransfer>.Fail(
                "CommunityNotFound", "Community not found.");

        if (!IsOwner(community, byPlayerId))
            return ServiceResult<OwnershipTransfer>.Fail(
                "NotAuthorized", "Only the current owner can transfer ownership.");

        if (newOwnerPlayerId == byPlayerId)
            return ServiceResult<OwnershipTransfer>.Fail(
                "SameOwner", "You are already the owner.");

        var newOwnerMembership = await members.GetMembershipAsync(communityId, newOwnerPlayerId, ct);
        if (newOwnerMembership is null)
            return ServiceResult<OwnershipTransfer>.Fail(
                "NotAMember", "The target player is not a member of this community.");

        var existing = await transfers.GetPendingByCommunityAsync(communityId, ct);
        if (existing is not null)
            return ServiceResult<OwnershipTransfer>.Fail(
                "TransferAlreadyPending",
                "A transfer is already pending for this community. Cancel or wait for it to expire.");

        var transfer = new OwnershipTransfer
        {
            CommunityId = communityId,
            FromPlayerId = byPlayerId,
            ToPlayerId = newOwnerPlayerId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + TransferExpiryWindow,
        };

        await transfers.CreateAsync(transfer, ct);

        var ownerPlayer = await players.GetByIdAsync(byPlayerId, ct);
        await notificationService.NotifyAsync(
            newOwnerPlayerId,
            "OwnershipTransferPending",
            $"{ownerPlayer?.DisplayName ?? "The current owner"} wants to hand you ownership of {community.Name}.",
            $"/communities/{communityId}",
            ct);

        return ServiceResult<OwnershipTransfer>.Ok(transfer);
    }

    public async Task<ServiceResult> RespondToTransferAsync(
        Guid transferId,
        Guid byPlayerId,
        bool accept,
        CancellationToken ct = default)
    {
        var transfer = await transfers.GetByIdAsync(transferId, ct);
        if (transfer is null)
            return ServiceResult.Fail("TransferNotFound", "Transfer request not found.");

        if (transfer.Status != OwnershipTransferStatus.Pending)
            return ServiceResult.Fail(
                "TransferNotPending", "That transfer is no longer pending.");

        if (DateTime.UtcNow > transfer.ExpiresAt)
        {
            transfer.Status = OwnershipTransferStatus.Expired;
            await transfers.UpdateAsync(transfer, ct);
            return ServiceResult.Fail("TransferExpired", "That transfer has expired.");
        }

        if (transfer.ToPlayerId != byPlayerId)
            return ServiceResult.Fail(
                "NotAuthorized", "Only the proposed new owner can respond.");

        if (!accept)
        {
            transfer.Status = OwnershipTransferStatus.Declined;
            transfer.RespondedAt = DateTime.UtcNow;
            await transfers.UpdateAsync(transfer, ct);
            return ServiceResult.Ok();
        }

        // Accept: swap roles (compensating-idempotent).
        var community = await communities.GetByIdAsync(transfer.CommunityId, ct);
        if (community is null)
            return ServiceResult.Fail("CommunityNotFound", "Community no longer exists.");

        var fromMembership = await members.GetMembershipAsync(transfer.CommunityId, transfer.FromPlayerId, ct);
        var toMembership = await members.GetMembershipAsync(transfer.CommunityId, transfer.ToPlayerId, ct);
        if (toMembership is null)
            return ServiceResult.Fail(
                "NotAMember", "You are no longer a member of this community.");

        // Old owner → Member
        if (fromMembership is not null)
        {
            await members.RemoveAsync(transfer.CommunityId, transfer.FromPlayerId, ct);
            await members.AddAsync(new CommunityMembership
            {
                CommunityId = transfer.CommunityId,
                PlayerId = transfer.FromPlayerId,
                Role = CommunityRole.Member,
                JoinedAt = fromMembership.JoinedAt,
                InvitedByPlayerId = fromMembership.InvitedByPlayerId,
            }, ct);
        }

        // New owner → Owner
        await members.RemoveAsync(transfer.CommunityId, transfer.ToPlayerId, ct);
        await members.AddAsync(new CommunityMembership
        {
            CommunityId = transfer.CommunityId,
            PlayerId = transfer.ToPlayerId,
            Role = CommunityRole.Owner,
            JoinedAt = toMembership.JoinedAt,
            InvitedByPlayerId = toMembership.InvitedByPlayerId,
        }, ct);

        community.OwnerPlayerId = transfer.ToPlayerId;
        await communities.UpdateAsync(community, ct);

        transfer.Status = OwnershipTransferStatus.Accepted;
        transfer.RespondedAt = DateTime.UtcNow;
        await transfers.UpdateAsync(transfer, ct);

        logger.LogInformation(
            "Ownership transfer {TransferId} accepted: community {CommunityId} {From} → {To}",
            transfer.TransferId, transfer.CommunityId, transfer.FromPlayerId, transfer.ToPlayerId);

        return ServiceResult.Ok();
    }

    public Task<OwnershipTransfer?> GetPendingTransferAsync(
        Guid communityId, CancellationToken ct = default)
        => transfers.GetPendingByCommunityAsync(communityId, ct);

    public Task<IReadOnlyList<OwnershipTransfer>> ListPendingTransfersForPlayerAsync(
        Guid playerId, CancellationToken ct = default)
        => transfers.ListPendingForTargetAsync(playerId, ct);

    // ── Invitations ─────────────────────────────────────────────────

    public async Task<ServiceResult<CommunityInvitation>> InviteAsync(
        Guid communityId,
        Guid invitedPlayerId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult<CommunityInvitation>.Fail(
                "CommunityNotFound", "Community not found.");

        if (!await IsOwnerOrAdminAsync(community, byPlayerId, ct))
            return ServiceResult<CommunityInvitation>.Fail(
                "NotAuthorized", "Only the community owner or an admin can invite members.");

        if (invitedPlayerId == byPlayerId)
            return ServiceResult<CommunityInvitation>.Fail(
                "SelfInvite", "You cannot invite yourself.");

        var target = await players.GetByIdAsync(invitedPlayerId, ct);
        if (target is null)
            return ServiceResult<CommunityInvitation>.Fail(
                "TargetNotFound", "That player was not found.");

        var existingMembership = await members.GetMembershipAsync(communityId, invitedPlayerId, ct);
        if (existingMembership is not null)
            return ServiceResult<CommunityInvitation>.Fail(
                "AlreadyMember", "That player is already a member.");

        var pending = await invitations.GetPendingAsync(communityId, invitedPlayerId, ct);
        if (pending is not null)
            return ServiceResult<CommunityInvitation>.Fail(
                "InviteAlreadyPending", "An invitation is already pending for that player.");

        var sentThisYear = await invitations.CountSentByInviterToTargetAsync(
            byPlayerId, invitedPlayerId, DateTime.UtcNow - InviteRateLimitWindow, ct);
        if (sentThisYear >= MaxInvitesPerInviterPerTargetPerYear)
            return ServiceResult<CommunityInvitation>.Fail(
                "InviteRateLimited",
                "You have invited this player the maximum number of times this year.");

        var invitation = new CommunityInvitation
        {
            CommunityId = communityId,
            InvitedPlayerId = invitedPlayerId,
            InvitedByPlayerId = byPlayerId,
            Status = CommunityInvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            await invitations.CreateAsync(invitation, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            return ServiceResult<CommunityInvitation>.Fail(
                "InviteAlreadyPending", "An invitation is already pending for that player.");
        }

        var inviter = await players.GetByIdAsync(byPlayerId, ct);
        await notificationService.NotifyAsync(
            invitedPlayerId,
            "CommunityInvitationReceived",
            $"{inviter?.DisplayName ?? "Someone"} invited you to join {community.Name}.",
            $"/friends",
            ct);

        return ServiceResult<CommunityInvitation>.Ok(invitation);
    }

    public async Task<ServiceResult> RespondToInvitationAsync(
        Guid invitationId,
        Guid byPlayerId,
        bool accept,
        CancellationToken ct = default)
    {
        var invitation = await invitations.GetByIdAsync(invitationId, ct);
        if (invitation is null)
            return ServiceResult.Fail("InvitationNotFound", "Invitation not found.");

        if (invitation.InvitedPlayerId != byPlayerId)
            return ServiceResult.Fail(
                "NotAuthorized", "Only the invited player can respond to an invitation.");

        if (invitation.Status != CommunityInvitationStatus.Pending)
            return ServiceResult.Fail(
                "InvitationNotPending", "That invitation is no longer pending.");

        var newStatus = accept
            ? CommunityInvitationStatus.Accepted
            : CommunityInvitationStatus.Declined;

        await invitations.UpdateStatusAsync(invitationId, newStatus, DateTime.UtcNow, ct);

        if (accept)
        {
            // Idempotent-compensating: if the membership already exists
            // for some reason, swallow the duplicate-key and still flip
            // the invitation.
            try
            {
                await members.AddAsync(new CommunityMembership
                {
                    CommunityId = invitation.CommunityId,
                    PlayerId = invitation.InvitedPlayerId,
                    Role = CommunityRole.Member,
                    JoinedAt = DateTime.UtcNow,
                    InvitedByPlayerId = invitation.InvitedByPlayerId,
                }, ct);
            }
            catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
            {
                logger.LogInformation(
                    "Membership already existed when accepting invitation {InvitationId}",
                    invitationId);
            }
        }

        return ServiceResult.Ok();
    }

    // ── Join requests ───────────────────────────────────────────────

    public async Task<ServiceResult<CommunityJoinRequest>> RequestToJoinAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult<CommunityJoinRequest>.Fail(
                "CommunityNotFound", "Community not found.");

        var existing = await members.GetMembershipAsync(communityId, playerId, ct);
        if (existing is not null)
            return ServiceResult<CommunityJoinRequest>.Fail(
                "AlreadyMember", "You are already a member.");

        var pending = await joinRequests.GetPendingAsync(communityId, playerId, ct);
        if (pending is not null)
            return ServiceResult<CommunityJoinRequest>.Fail(
                "JoinRequestAlreadyPending", "You already have a pending request to join.");

        var request = new CommunityJoinRequest
        {
            CommunityId = communityId,
            PlayerId = playerId,
            Status = CommunityJoinRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            await joinRequests.CreateAsync(request, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            return ServiceResult<CommunityJoinRequest>.Fail(
                "JoinRequestAlreadyPending", "You already have a pending request to join.");
        }

        return ServiceResult<CommunityJoinRequest>.Ok(request);
    }

    public async Task<ServiceResult<CommunityMembership>> ApproveJoinRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var request = await joinRequests.GetByIdAsync(requestId, ct);
        if (request is null)
            return ServiceResult<CommunityMembership>.Fail(
                "JoinRequestNotFound", "Join request not found.");

        if (request.Status != CommunityJoinRequestStatus.Pending)
            return ServiceResult<CommunityMembership>.Fail(
                "JoinRequestNotPending", "That join request is no longer pending.");

        var community = await communities.GetByIdAsync(request.CommunityId, ct);
        if (community is null)
            return ServiceResult<CommunityMembership>.Fail(
                "CommunityNotFound", "Community not found.");

        if (!await IsOwnerOrAdminAsync(community, byPlayerId, ct))
            return ServiceResult<CommunityMembership>.Fail(
                "NotAuthorized", "Only the community owner or an admin can approve join requests.");

        var membership = new CommunityMembership
        {
            CommunityId = request.CommunityId,
            PlayerId = request.PlayerId,
            Role = CommunityRole.Member,
            JoinedAt = DateTime.UtcNow,
            InvitedByPlayerId = byPlayerId,
        };

        try
        {
            await members.AddAsync(membership, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            logger.LogInformation(
                "Membership already existed when approving join request {RequestId}", requestId);
            membership = (await members.GetMembershipAsync(request.CommunityId, request.PlayerId, ct))
                ?? membership;
        }

        await joinRequests.UpdateStatusAsync(
            requestId, CommunityJoinRequestStatus.Approved, DateTime.UtcNow, byPlayerId, ct);

        return ServiceResult<CommunityMembership>.Ok(membership);
    }

    public async Task<ServiceResult> DenyJoinRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var request = await joinRequests.GetByIdAsync(requestId, ct);
        if (request is null)
            return ServiceResult.Fail("JoinRequestNotFound", "Join request not found.");

        if (request.Status != CommunityJoinRequestStatus.Pending)
            return ServiceResult.Fail(
                "JoinRequestNotPending", "That join request is no longer pending.");

        var community = await communities.GetByIdAsync(request.CommunityId, ct);
        if (community is null)
            return ServiceResult.Fail("CommunityNotFound", "Community not found.");

        if (!await IsOwnerOrAdminAsync(community, byPlayerId, ct))
            return ServiceResult.Fail(
                "NotAuthorized", "Only the community owner or an admin can deny join requests.");

        await joinRequests.UpdateStatusAsync(
            requestId, CommunityJoinRequestStatus.Denied, DateTime.UtcNow, byPlayerId, ct);

        return ServiceResult.Ok();
    }

    // ── Public self-join ────────────────────────────────────────────

    public async Task<ServiceResult<CommunityMembership>> JoinPublicAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult<CommunityMembership>.Fail(
                "CommunityNotFound", "Community not found.");

        if (community.Visibility != CommunityVisibility.Public)
            return ServiceResult<CommunityMembership>.Fail(
                "PrivateCommunityRequiresInvite",
                "This community is private. Ask the owner for an invitation.");

        var existing = await members.GetMembershipAsync(communityId, playerId, ct);
        if (existing is not null)
            return ServiceResult<CommunityMembership>.Fail(
                "AlreadyMember", "You are already a member.");

        var membership = new CommunityMembership
        {
            CommunityId = communityId,
            PlayerId = playerId,
            Role = CommunityRole.Member,
            JoinedAt = DateTime.UtcNow,
        };

        try
        {
            await members.AddAsync(membership, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            return ServiceResult<CommunityMembership>.Fail(
                "AlreadyMember", "You are already a member.");
        }

        return ServiceResult<CommunityMembership>.Ok(membership);
    }

    // ── Membership management ──────────────────────────────────────

    public async Task<ServiceResult> LeaveAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult.Fail("CommunityNotFound", "Community not found.");

        var membership = await members.GetMembershipAsync(communityId, playerId, ct);
        if (membership is null)
            return ServiceResult.Fail("NotAMember", "You are not a member of this community.");

        if (membership.Role == CommunityRole.Owner)
            return ServiceResult.Fail(
                "OwnerCannotLeave",
                "Transfer ownership before leaving, or delete the community.");

        await members.RemoveAsync(communityId, playerId, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveMemberAsync(
        Guid communityId,
        Guid targetPlayerId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult.Fail("CommunityNotFound", "Community not found.");

        var callerMembership = await members.GetMembershipAsync(communityId, byPlayerId, ct);
        var isCallerOwner = IsOwner(community, byPlayerId);
        var isCallerAdmin = callerMembership?.Role == CommunityRole.Admin;

        if (!isCallerOwner && !isCallerAdmin)
            return ServiceResult.Fail(
                "NotAuthorized", "Only the community owner or an admin can remove members.");

        if (targetPlayerId == byPlayerId)
            return ServiceResult.Fail(
                "CannotRemoveSelf",
                isCallerOwner
                    ? "Owners cannot remove themselves."
                    : "Use the Leave button to leave the community.");

        var membership = await members.GetMembershipAsync(communityId, targetPlayerId, ct);
        if (membership is null)
            return ServiceResult.Fail("NotAMember", "That player is not a member.");

        if (membership.Role == CommunityRole.Owner)
            return ServiceResult.Fail(
                "CannotRemoveOwner", "You cannot remove the community owner.");

        // Admins cannot remove other Admins — only the Owner can.
        if (isCallerAdmin && membership.Role == CommunityRole.Admin)
            return ServiceResult.Fail(
                "AdminCannotRemoveAdmin",
                "Only the community owner can remove an admin.");

        await members.RemoveAsync(communityId, targetPlayerId, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetMemberRoleAsync(
        Guid communityId,
        Guid targetPlayerId,
        CommunityRole newRole,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null)
            return ServiceResult.Fail("CommunityNotFound", "Community not found.");

        if (!IsOwner(community, byPlayerId))
            return ServiceResult.Fail(
                "NotAuthorized", "Only the community owner can change roles.");

        if (newRole == CommunityRole.Owner)
            return ServiceResult.Fail(
                "UseTransferOwnership",
                "Use TransferOwnershipAsync to change the community owner.");

        if (targetPlayerId == byPlayerId)
            return ServiceResult.Fail(
                "CannotDemoteSelf",
                "Use TransferOwnershipAsync to step down as owner.");

        var membership = await members.GetMembershipAsync(communityId, targetPlayerId, ct);
        if (membership is null)
            return ServiceResult.Fail("NotAMember", "That player is not a member.");

        membership.Role = newRole;

        // No in-place update on the member repo yet; remove + re-add.
        await members.RemoveAsync(communityId, targetPlayerId, ct);
        await members.AddAsync(membership, ct);

        return ServiceResult.Ok();
    }

    // ── Reads ───────────────────────────────────────────────────────

    public async Task<Community?> GetForViewerAsync(
        Guid communityId,
        Guid? viewerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null) return null;

        if (community.Visibility == CommunityVisibility.Public)
            return community;

        // Private: only members may see it exists.
        if (viewerId is null) return null;
        var membership = await members.GetMembershipAsync(communityId, viewerId.Value, ct);
        return membership is null ? null : community;
    }

    public async Task<IReadOnlyList<CommunityMemberView>> ListMembersAsync(
        Guid communityId,
        Guid? viewerId,
        CancellationToken ct = default)
    {
        var community = await communities.GetByIdAsync(communityId, ct);
        if (community is null) return Array.Empty<CommunityMemberView>();

        var isViewerMember = viewerId is not null
            && await members.GetMembershipAsync(communityId, viewerId.Value, ct) is not null;

        // Private community + non-member viewer: empty list (caller should 404).
        if (community.Visibility == CommunityVisibility.Private && !isViewerMember)
            return Array.Empty<CommunityMemberView>();

        var rows = await members.ListMembersAsync(communityId, skip: 0, limit: int.MaxValue, ct);
        var result = new List<CommunityMemberView>(rows.Count);

        foreach (var row in rows)
        {
            var player = await players.GetByIdAsync(row.PlayerId, ct);
            if (player is null) continue;

            var avatarUrl = player.Avatar is not null ? $"/api/avatars/{player.PlayerId}" : null;

            if (isViewerMember)
            {
                result.Add(new CommunityMemberView(
                    player.PlayerId,
                    player.DisplayName,
                    avatarUrl,
                    row.Role,
                    row.JoinedAt));
            }
            else
            {
                // Public-community non-member: display name + avatar only.
                result.Add(new CommunityMemberView(
                    player.PlayerId,
                    player.DisplayName,
                    avatarUrl,
                    CommunityRole.Member,
                    JoinedAt: null));
            }
        }

        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<Community>> ListCommunitiesForPlayerAsync(
        Guid playerId,
        CancellationToken ct = default)
    {
        var memberships = await members.ListCommunitiesForPlayerAsync(playerId, ct);
        var result = new List<Community>(memberships.Count);
        foreach (var m in memberships)
        {
            var c = await communities.GetByIdAsync(m.CommunityId, ct);
            if (c is not null) result.Add(c);
        }

        return result
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<Community>> BrowsePublicAsync(
        string? namePrefix,
        int limit = 20,
        CancellationToken ct = default)
    {
        var prefix = (namePrefix ?? string.Empty).Trim();
        return await communities.SearchPublicByNameAsync(prefix, limit, ct);
    }

    public async Task<bool> IsMemberAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default)
    {
        var m = await members.GetMembershipAsync(communityId, playerId, ct);
        return m is not null;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static bool IsOwner(Community community, Guid playerId)
        => community.OwnerPlayerId == playerId;

    /// <summary>
    /// Returns true when the caller is the Owner or holds the Admin role.
    /// Used for authz gates that Sprint 4 widened from Owner-only to
    /// Owner + Admin (invite, approve/deny join requests, remove member).
    /// </summary>
    private async Task<bool> IsOwnerOrAdminAsync(
        Community community, Guid playerId, CancellationToken ct)
    {
        if (IsOwner(community, playerId)) return true;
        var m = await members.GetMembershipAsync(community.CommunityId, playerId, ct);
        return m?.Role == CommunityRole.Admin;
    }

    private static string? TrimDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var trimmed = description.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static bool IsDuplicateKey(MongoDB.Driver.MongoWriteException ex)
        => ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey;
}
