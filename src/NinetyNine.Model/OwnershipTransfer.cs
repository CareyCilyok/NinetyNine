namespace NinetyNine.Model;

/// <summary>
/// A pending request from the current owner of a community to hand
/// ownership to another member. The target must accept within the
/// expiry window; declining or expiring leaves the original owner in
/// place.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 4 S4.3.</para>
/// </summary>
public class OwnershipTransfer
{
    public Guid TransferId { get; set; } = Guid.NewGuid();
    public Guid CommunityId { get; set; }

    /// <summary>Current owner initiating the transfer.</summary>
    public Guid FromPlayerId { get; set; }

    /// <summary>Target member who must accept.</summary>
    public Guid ToPlayerId { get; set; }

    public OwnershipTransferStatus Status { get; set; } = OwnershipTransferStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public enum OwnershipTransferStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3,
}
