using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Community lifecycle service. Owns creation, membership, invitations,
/// join requests, leave / remove / transfer-ownership, and all
/// authorization checks so UI code is free of business rules.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 2 S2.1.</para>
/// </summary>
public interface ICommunityService
{
    /// <summary>
    /// Create a player-owned community. The caller becomes <c>Owner</c>
    /// automatically via a new <see cref="CommunityMembership"/>.
    /// Enforces:
    /// <list type="bullet">
    /// <item>Max 10 communities per owner (<c>CommunityCapExceeded</c>).</item>
    /// <item>Case-insensitive unique name (<c>CommunityNameTaken</c>).</item>
    /// <item>Name 2–40 chars, slug non-empty (<c>InvalidCommunityInput</c>).</item>
    /// </list>
    /// </summary>
    Task<ServiceResult<Community>> CreatePlayerOwnedAsync(
        Guid ownerPlayerId,
        string name,
        string slug,
        string? description,
        CommunityVisibility visibility,
        CancellationToken ct = default);

    /// <summary>
    /// Update editable fields (name, slug, description, visibility).
    /// Owner-only. Only fields with non-null values in
    /// <paramref name="changes"/> are applied.
    /// </summary>
    Task<ServiceResult<Community>> UpdateAsync(
        Guid communityId,
        Guid byPlayerId,
        CommunityUpdate changes,
        CancellationToken ct = default);

    /// <summary>
    /// Delete a community and cascade-remove every membership and
    /// pending invitation / join request. Owner-only.
    /// Also nulls out <c>Venue.CommunityId</c> on affiliated venues.
    /// </summary>
    Task<ServiceResult> DeleteAsync(
        Guid communityId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Initiate an ownership transfer to another current member. Owner-only.
    /// Creates a Pending <see cref="OwnershipTransfer"/> with a 7-day
    /// expiry. Only one pending transfer per community is allowed.
    /// The target must accept via <see cref="RespondToTransferAsync"/>.
    /// <para>Sprint 4 S4.3 replaced the immediate-swap v1.0 flow.</para>
    /// </summary>
    Task<ServiceResult<OwnershipTransfer>> TransferOwnershipAsync(
        Guid communityId,
        Guid newOwnerPlayerId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// The transfer target accepts or declines. On accept, roles swap
    /// atomically (compensating-idempotent). On decline, the transfer
    /// is marked Declined and the original owner remains.
    /// </summary>
    Task<ServiceResult> RespondToTransferAsync(
        Guid transferId,
        Guid byPlayerId,
        bool accept,
        CancellationToken ct = default);

    /// <summary>
    /// Returns any pending ownership transfer for the given community,
    /// or null if none exists.
    /// </summary>
    Task<OwnershipTransfer?> GetPendingTransferAsync(
        Guid communityId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all pending ownership transfers where the given player
    /// is the proposed new owner.
    /// </summary>
    Task<IReadOnlyList<OwnershipTransfer>> ListPendingTransfersForPlayerAsync(
        Guid playerId,
        CancellationToken ct = default);

    // ── Invitations (community → player) ────────────────────────────

    /// <summary>
    /// Invite a player to join. Owner or Admin (Sprint 4 S4.2).
    /// Enforces:
    /// <list type="bullet">
    /// <item>Max 5 invites from any inviter to any target in a rolling 365 days (<c>InviteRateLimited</c>).</item>
    /// <item>Target must not already be a member (<c>AlreadyMember</c>).</item>
    /// <item>No duplicate Pending invite (<c>InviteAlreadyPending</c>).</item>
    /// </list>
    /// </summary>
    Task<ServiceResult<CommunityInvitation>> InviteAsync(
        Guid communityId,
        Guid invitedPlayerId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Respond to a Pending invitation. If <paramref name="accept"/> is
    /// true, creates a <see cref="CommunityMembership"/> for the invitee.
    /// Only the invitee may respond.
    /// </summary>
    Task<ServiceResult> RespondToInvitationAsync(
        Guid invitationId,
        Guid byPlayerId,
        bool accept,
        CancellationToken ct = default);

    // ── Join requests (player → private community) ─────────────────

    /// <summary>
    /// Submit a join request to a private community. Public communities
    /// should use <see cref="JoinPublicAsync"/> instead. Prevents dupes
    /// and same-direction races via the partial unique index.
    /// </summary>
    Task<ServiceResult<CommunityJoinRequest>> RequestToJoinAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>Approve a Pending join request. Owner or Admin.</summary>
    Task<ServiceResult<CommunityMembership>> ApproveJoinRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>Deny a Pending join request. Owner or Admin.</summary>
    Task<ServiceResult> DenyJoinRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default);

    // ── Public community self-join ─────────────────────────────────

    /// <summary>
    /// Join a public community with one click. Rejected for private
    /// communities (<c>PrivateCommunityRequiresInvite</c>).
    /// </summary>
    Task<ServiceResult<CommunityMembership>> JoinPublicAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default);

    // ── Membership management ─────────────────────────────────────

    /// <summary>
    /// Leave a community you are a member of. The sole Owner cannot
    /// leave — ownership must be transferred first.
    /// </summary>
    Task<ServiceResult> LeaveAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>
    /// Remove a member from a community. Owner or Admin (Sprint 4).
    /// Admins cannot remove Owner or other Admins. Owner cannot be
    /// removed (must transfer first).
    /// </summary>
    Task<ServiceResult> RemoveMemberAsync(
        Guid communityId,
        Guid targetPlayerId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Change a member's role. Owner-only. Cannot demote self — use
    /// <see cref="TransferOwnershipAsync"/> instead.
    /// </summary>
    Task<ServiceResult> SetMemberRoleAsync(
        Guid communityId,
        Guid targetPlayerId,
        CommunityRole newRole,
        Guid byPlayerId,
        CancellationToken ct = default);

    // ── Reads ─────────────────────────────────────────────────────

    /// <summary>
    /// Fetch a community with per-viewer access control:
    /// <list type="bullet">
    /// <item>Public: visible to anyone.</item>
    /// <item>Private: returns null for non-members (the route guard
    /// should render 404).</item>
    /// </list>
    /// </summary>
    Task<Community?> GetForViewerAsync(
        Guid communityId,
        Guid? viewerId,
        CancellationToken ct = default);

    /// <summary>
    /// List the members of a community, scoped to what the viewer is
    /// allowed to see. Members see everyone; public-community
    /// non-members see display names only; private-community
    /// non-members see nothing (empty list).
    /// </summary>
    Task<IReadOnlyList<CommunityMemberView>> ListMembersAsync(
        Guid communityId,
        Guid? viewerId,
        CancellationToken ct = default);

    /// <summary>
    /// List the communities a player currently belongs to. Sorted by
    /// community name.
    /// </summary>
    Task<IReadOnlyList<Community>> ListCommunitiesForPlayerAsync(
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>
    /// Browse public communities by name prefix. Never returns private
    /// communities, even if the caller is a member — use
    /// <see cref="ListCommunitiesForPlayerAsync"/> for "my communities".
    /// </summary>
    Task<IReadOnlyList<Community>> BrowsePublicAsync(
        string? namePrefix,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Cheap membership probe used by route guards. Returns true when
    /// the player currently belongs to the community (any role).
    /// </summary>
    Task<bool> IsMemberAsync(
        Guid communityId,
        Guid playerId,
        CancellationToken ct = default);

    // ── Hierarchy (v0.8.0) ──────────────────────────────────────────────

    /// <summary>
    /// Returns the full community hierarchy as a forest of trees rooted
    /// at every community whose <see cref="Community.ParentCommunityId"/>
    /// is null. In the v0.8.x seed there is exactly one root: "Global".
    /// Public-only filter applies — private communities are pruned from
    /// the returned tree (their children re-root if otherwise visible).
    /// </summary>
    Task<IReadOnlyList<CommunityNode>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets a community's parent. Validates that:
    /// <list type="bullet">
    ///   <item>The community exists.</item>
    ///   <item>If <paramref name="parentCommunityId"/> is non-null, it
    ///     points to an existing community.</item>
    ///   <item>The new parent does not equal the community itself.</item>
    ///   <item>Setting the parent does not introduce a cycle (the
    ///     proposed parent's ancestor chain must not contain the
    ///     community being parented).</item>
    /// </list>
    /// Pass <c>null</c> for <paramref name="parentCommunityId"/> to make
    /// the community a root.
    /// </summary>
    Task<ServiceResult<Community>> SetParentAsync(
        Guid communityId,
        Guid? parentCommunityId,
        Guid byPlayerId,
        CancellationToken ct = default);
}

/// <summary>
/// One node in the community hierarchy tree returned by
/// <see cref="ICommunityService.GetTreeAsync"/>. <see cref="Children"/>
/// is sorted by community name.
/// </summary>
public sealed record CommunityNode(
    Community Community,
    IReadOnlyList<CommunityNode> Children);

/// <summary>
/// Mutable-field changes passed to <see cref="ICommunityService.UpdateAsync"/>.
/// Only non-null fields are applied.
/// </summary>
public sealed record CommunityUpdate(
    string? Name = null,
    string? Slug = null,
    string? Description = null,
    CommunityVisibility? Visibility = null);

/// <summary>
/// Viewer-scoped projection of a <see cref="CommunityMembership"/>
/// joined with the underlying <see cref="Player"/>. What's populated
/// depends on the viewer's access — non-members of public communities
/// see only DisplayName and avatar; members see role and JoinedAt too.
/// </summary>
public sealed record CommunityMemberView(
    Guid PlayerId,
    string DisplayName,
    string? AvatarUrl,
    CommunityRole Role,
    DateTime? JoinedAt);
