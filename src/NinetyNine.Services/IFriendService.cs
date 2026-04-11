using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Friendship lifecycle service. Encapsulates every invariant and rate
/// limit from the backend and security plans so UI code is free of
/// business rules. See <c>docs/plans/friends-communities-v1.md</c>
/// Sprint 1 S1.1 for the locked decisions this implementation encodes.
/// </summary>
public interface IFriendService
{
    /// <summary>
    /// Send a friend request. Enforces:
    /// <list type="bullet">
    /// <item>Cannot friend self (<c>SelfFriendship</c>).</item>
    /// <item>Target must exist (<c>TargetNotFound</c>).</item>
    /// <item>Must not already be friends (<c>AlreadyFriends</c>).</item>
    /// <item>No existing Pending request in either direction
    /// (<c>RequestAlreadyPending</c>).</item>
    /// <item>Per-sender rate limits: 10 outbound pending at once, 20
    /// created in last 24h, 3 to the same target in last 30d
    /// (<c>FriendRequestRateLimited</c>).</item>
    /// <item>90-day cooldown after a target declined the most recent
    /// request (<c>FriendRequestCooldown</c>).</item>
    /// </list>
    /// </summary>
    Task<ServiceResult<FriendRequest>> SendRequestAsync(
        Guid fromPlayerId,
        Guid toPlayerId,
        string? message = null,
        CancellationToken ct = default);

    /// <summary>
    /// Cancel a Pending request you previously sent. Fails with
    /// <c>RequestNotFound</c> or <c>NotAuthorized</c> if the caller
    /// is not the sender.
    /// </summary>
    Task<ServiceResult> CancelRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Accept a Pending request. The caller must be the recipient.
    /// Creates the <see cref="Friendship"/> and marks the request
    /// Accepted in a compensating-idempotent sequence so a mid-operation
    /// crash never leaves the pair in an inconsistent state.
    /// </summary>
    Task<ServiceResult<Friendship>> AcceptRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Decline a Pending request. Caller must be the recipient. Starts
    /// the 90-day re-request cooldown for the same direction.
    /// </summary>
    Task<ServiceResult> DeclineRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Remove an existing friendship. Bidirectional: after this returns,
    /// neither party sees the other in their friends list. Idempotent
    /// — a no-op if they were not friends.
    /// </summary>
    Task<ServiceResult> RemoveFriendAsync(
        Guid playerId,
        Guid otherPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// List every player who is a mutual friend of the caller.
    /// </summary>
    Task<IReadOnlyList<Player>> ListFriendsAsync(
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>
    /// List Pending requests received by the caller, newest first.
    /// </summary>
    Task<IReadOnlyList<FriendRequest>> ListIncomingRequestsAsync(
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>
    /// List Pending requests sent by the caller, newest first.
    /// </summary>
    Task<IReadOnlyList<FriendRequest>> ListOutgoingRequestsAsync(
        Guid playerId,
        CancellationToken ct = default);

    /// <summary>
    /// Constant-time-ish check: are two players mutual friends?
    /// Argument order does not matter.
    /// </summary>
    Task<bool> AreFriendsAsync(
        Guid playerAId,
        Guid playerBId,
        CancellationToken ct = default);

    /// <summary>
    /// The relationship between a viewer and a target player. Used by
    /// profile pages to pick the right action button (Send / Cancel /
    /// Accept / Remove / nothing for self).
    /// </summary>
    Task<RelationshipState> GetRelationshipAsync(
        Guid viewerId,
        Guid targetId,
        CancellationToken ct = default);
}

/// <summary>
/// Viewer-to-target relationship state used by friendship UI surfaces.
/// Order deliberately ranges from "no relationship" to "self" so the
/// UI can dispatch on a single enum.
/// </summary>
public enum RelationshipState
{
    /// <summary>No friendship, no pending requests in either direction.</summary>
    None = 0,

    /// <summary>Viewer has a Pending request outstanding to the target.</summary>
    RequestSent = 1,

    /// <summary>Target has a Pending request outstanding to the viewer.</summary>
    RequestReceived = 2,

    /// <summary>Viewer and target are mutual friends.</summary>
    Friends = 3,

    /// <summary>Viewer is viewing their own profile.</summary>
    Self = 4,
}
