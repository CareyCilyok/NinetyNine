namespace NinetyNine.Model;

/// <summary>
/// An outbound invitation from a <see cref="Community"/> to a prospective
/// member. Created by a community Owner or Admin via
/// <c>ICommunityService.InviteAsync</c>. The recipient accepts, declines,
/// or lets it expire.
/// <para>
/// Invitations auto-expire 14 days after <see cref="CreatedAt"/>; the
/// <c>DataSeeder</c> heal pass (Sprint 4) sweeps old ones into
/// <see cref="CommunityInvitationStatus.Expired"/>.
/// </para>
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.2.</para>
/// </summary>
public class CommunityInvitation
{
    public Guid InvitationId { get; set; } = Guid.NewGuid();

    public Guid CommunityId { get; set; }

    /// <summary>Player being invited.</summary>
    public Guid InvitedPlayerId { get; set; }

    /// <summary>Community Owner or Admin who issued the invite.</summary>
    public Guid InvitedByPlayerId { get; set; }

    public CommunityInvitationStatus Status { get; set; } = CommunityInvitationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC when the invitation reached a terminal state.</summary>
    public DateTime? RespondedAt { get; set; }
}

/// <summary>
/// Lifecycle state of a <see cref="CommunityInvitation"/>.
/// </summary>
public enum CommunityInvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Revoked = 3,
    Expired = 4,
}
