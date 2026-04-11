using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="CommunityInvitation"/> documents.
/// Invitations are outbound (community → player) and have a short
/// Pending → Accepted/Declined/Revoked/Expired lifecycle.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 2 S2.1.</para>
/// </summary>
public interface ICommunityInvitationRepository
{
    Task<CommunityInvitation?> GetByIdAsync(Guid invitationId, CancellationToken ct = default);

    /// <summary>
    /// Returns the single Pending invitation between a community and a
    /// player (if any). Used to prevent duplicate pending invites.
    /// </summary>
    Task<CommunityInvitation?> GetPendingAsync(
        Guid communityId,
        Guid invitedPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists invitations received by a player, optionally filtered by
    /// status. Sorted newest-first.
    /// </summary>
    Task<IReadOnlyList<CommunityInvitation>> ListByInviteeAsync(
        Guid invitedPlayerId,
        CommunityInvitationStatus? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists invitations sent by a specific inviter to a specific player
    /// since the given cutoff. Used to enforce the "5 invites per inviter
    /// per target per year" rate limit.
    /// </summary>
    Task<long> CountSentByInviterToTargetAsync(
        Guid inviterPlayerId,
        Guid invitedPlayerId,
        DateTime sinceUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Lists invitations for a specific community, optionally filtered
    /// by status. Used on community settings pages.
    /// </summary>
    Task<IReadOnlyList<CommunityInvitation>> ListByCommunityAsync(
        Guid communityId,
        CommunityInvitationStatus? status = null,
        CancellationToken ct = default);

    Task CreateAsync(CommunityInvitation invitation, CancellationToken ct = default);

    /// <summary>
    /// Transitions an invitation to a terminal state, stamping
    /// <c>RespondedAt</c>. No-op if already terminal.
    /// </summary>
    Task UpdateStatusAsync(
        Guid invitationId,
        CommunityInvitationStatus status,
        DateTime respondedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Expires Pending invitations older than the cutoff. Returns how
    /// many were updated. Used by the Sprint 4 heal pass.
    /// </summary>
    Task<long> SweepExpiredAsync(DateTime olderThan, CancellationToken ct = default);
}
